using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Xaml;
using PowerMouse.App.Diagnostics;
using PowerMouse.App.Services;

namespace PowerMouse.App;

public sealed partial class App : Application, IDisposable
{
    private const string MutexName = @"Local\PowerMouse.SingleInstance";

    private Mutex? instanceMutex;
    private MainWindow? mainWindow;
    private TrayIconService? trayIcon;
    private CursorIndicatorService? cursorIndicator;
    private ClickHintsService? clickHints;
    private PowerMouseController? controller;
    private SingleInstanceCommandService? singleInstanceCommands;
    private bool shuttingDown;
    private bool restartingAsAdministrator;

    public App()
    {
        Logger.InitializeLogger();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "Unknown";
        Logger.LogInfo(
            $"PowerMouse {version} is starting. " +
            $"Architecture={RuntimeInformation.ProcessArchitecture}; Framework={RuntimeInformation.FrameworkDescription}.");

        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    internal static App CurrentApp => (App)Current;

    internal PowerMouseController Controller => controller ?? throw new InvalidOperationException("PowerMouse has not finished starting.");

    internal bool IsShuttingDown => shuttingDown;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = args;
        try
        {
            AdministratorRestartService.CompleteRestartIfRequested(Environment.GetCommandLineArgs());
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            Logger.LogError("The administrator restart could not be completed.", exception);
            Logger.Shutdown();
            Exit();
            return;
        }
        var launchCommand = GetLaunchCommand();
        instanceMutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            var command = launchCommand ?? SingleInstanceCommand.ShowSettings;
            var sent = SingleInstanceCommandService.TrySend(command);
            Logger.LogInfo(
                $"Another PowerMouse instance is already running; the {command} command " +
                $"was {(sent ? "sent" : "not delivered")} and this process will exit.");
            instanceMutex.Dispose();
            instanceMutex = null;
            Logger.Shutdown();
            Exit();
            return;
        }

        if (launchCommand == SingleInstanceCommand.Shutdown)
        {
            Logger.LogInfo("No existing PowerMouse instance needs to be shut down.");
            Dispose();
            Logger.Shutdown();
            Exit();
            return;
        }

        controller = new PowerMouseController();
        mainWindow = new MainWindow();
        singleInstanceCommands = new SingleInstanceCommandService(
            mainWindow.DispatcherQueue,
            HandleSingleInstanceCommand);

        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
        trayIcon = new TrayIconService(windowHandle)
        {
            IsEnabledProvider = () => Controller.IsEnabled,
        };
        cursorIndicator = new CursorIndicatorService();

        trayIcon.OpenRequested += mainWindow.ShowSettings;
        trayIcon.ToggleRequested += Controller.Toggle;
        trayIcon.ExitRequested += Shutdown;
        Controller.EnabledChanged += OnEnabledChanged;
        Controller.PreferencesChanged += OnPreferencesChanged;
        Controller.Error += OnControllerError;

        try
        {
            clickHints = new ClickHintsService(Controller.Settings, mainWindow.DispatcherQueue);
            clickHints.Notice += OnClickHintsNotice;
            Controller.SetKeyInterceptor(clickHints.ProcessKey);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Logger.LogError("Mouse Hints could not be initialized.", exception);
            trayIcon.ShowNotification("Mouse Hints is unavailable", exception.Message);
        }

        Controller.Start();
        mainWindow.SetEnabledStatus(Controller.IsEnabled);
        UpdatePointerIndicator();

        if (launchCommand == SingleInstanceCommand.ShowSettings)
        {
            mainWindow.ShowSettings();
        }
        else if (launchCommand == SingleInstanceCommand.InspectHints)
        {
            clickHints?.InspectForeground();
        }

        Logger.LogInfo($"PowerMouse started successfully. Emulation enabled={Controller.IsEnabled}.");
        if (Controller.Settings.AlwaysRunAsAdministrator && !AdministratorRestartService.IsAdministrator &&
            !Environment.GetCommandLineArgs().Contains("--dont-elevate", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!await RestartAsAdministratorAsync(showSettings: launchCommand == SingleInstanceCommand.ShowSettings))
                    trayIcon?.ShowNotification("Administrator restart canceled", "PowerMouse is running with standard permissions for this session.");
            }
            catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
            {
                OnControllerError(exception);
            }
        }
    }

    internal async Task<bool> RestartAsAdministratorAsync(bool showSettings = true)
    {
        if (restartingAsAdministrator || shuttingDown) return false;
        restartingAsAdministrator = true;
        try
        {
            if (!await AdministratorRestartService.RequestRestartAsync(showSettings: showSettings)) return false;
            Logger.LogInfo("The administrator instance is ready; shutting down this instance for restart.");
            Shutdown();
            return true;
        }
        finally { restartingAsAdministrator = false; }
    }

    internal void Shutdown()
    {
        if (shuttingDown)
        {
            return;
        }

        shuttingDown = true;
        Logger.LogInfo("PowerMouse is shutting down.");
        Dispose();
        mainWindow?.Close();
        Logger.LogInfo("PowerMouse shutdown completed.");
        Logger.Shutdown();
        Exit();
    }

    public void Dispose()
    {
        singleInstanceCommands?.Dispose();
        singleInstanceCommands = null;

        if (controller is not null)
        {
            controller.SetKeyInterceptor(null);
        }

        clickHints?.Dispose();
        clickHints = null;
        cursorIndicator?.Dispose();
        cursorIndicator = null;
        trayIcon?.Dispose();
        trayIcon = null;
        controller?.Dispose();
        controller = null;

        if (instanceMutex is not null)
        {
            instanceMutex.ReleaseMutex();
            instanceMutex.Dispose();
            instanceMutex = null;
        }

        GC.SuppressFinalize(this);
    }

    private static SingleInstanceCommand? GetLaunchCommand()
    {
        var arguments = Environment.GetCommandLineArgs();
        if (arguments.Any(argument => string.Equals(argument, "--inspect-hints", StringComparison.OrdinalIgnoreCase)))
        {
            return SingleInstanceCommand.InspectHints;
        }
        if (arguments.Any(argument => string.Equals(argument, "--shutdown", StringComparison.OrdinalIgnoreCase)))
        {
            return SingleInstanceCommand.Shutdown;
        }

        return arguments.Any(argument => string.Equals(argument, "--show-settings", StringComparison.OrdinalIgnoreCase))
            ? SingleInstanceCommand.ShowSettings
            : null;
    }

    private void HandleSingleInstanceCommand(SingleInstanceCommand command)
    {
        Logger.LogInfo($"The running instance received the {command} command.");
        switch (command)
        {
            case SingleInstanceCommand.InspectHints:
                clickHints?.InspectForeground();
                break;
            case SingleInstanceCommand.ShowSettings:
                mainWindow?.ShowSettings();
                break;
            case SingleInstanceCommand.Shutdown:
                Shutdown();
                break;
            default:
                Logger.LogWarning($"The running instance ignored unsupported command {command}.");
                break;
        }
    }

    private void OnEnabledChanged(bool enabled)
    {
        Logger.LogInfo($"Mouse emulation changed to {(enabled ? "enabled" : "disabled")}.");
        mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            mainWindow.SetEnabledStatus(enabled);
            UpdatePointerIndicator();
            if (Controller.Settings.ShowNotifications)
            {
                trayIcon?.ShowNotification("PowerMouse", enabled ? "Mouse emulation is on." : "Mouse emulation is off.");
            }
        });
    }

    private void UpdatePointerIndicator()
    {
        if (mainWindow is null)
        {
            return;
        }

        mainWindow.DispatcherQueue.TryEnqueue(() =>
            cursorIndicator?.Update(Controller.IsEnabled, Controller.Settings.ShowPointerIndicator));
    }

    private void OnPreferencesChanged()
    {
        UpdatePointerIndicator();
        clickHints?.ApplySettings();
    }

    private void OnControllerError(Exception exception)
    {
        Logger.LogError("A controller operation failed.", exception);
        mainWindow?.DispatcherQueue.TryEnqueue(() =>
            trayIcon?.ShowNotification("PowerMouse encountered a problem", exception.Message));
    }

    private void OnClickHintsNotice(string message)
    {
        Logger.LogWarning(message);
        mainWindow?.DispatcherQueue.TryEnqueue(() =>
            trayIcon?.ShowNotification("Mouse Hints", message));
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        _ = sender;
        Logger.LogError("An unhandled WinUI exception occurred.", args.Exception);
        args.Handled = true;
        trayIcon?.ShowNotification("PowerMouse encountered a problem", args.Message);
    }

    private static void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs args)
    {
        _ = sender;
        if (args.ExceptionObject is Exception exception)
        {
            Logger.LogError("An unhandled application exception occurred.", exception);
        }
        else
        {
            Logger.LogError($"An unhandled application error occurred: {args.ExceptionObject}");
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        _ = sender;
        Logger.LogError("An unobserved task exception occurred.", args.Exception);
    }
}
