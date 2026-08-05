using System.Threading;
using Microsoft.UI.Xaml;
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
    private bool shuttingDown;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    internal static App CurrentApp => (App)Current;

    internal PowerMouseController Controller => controller ?? throw new InvalidOperationException("PowerMouse has not finished starting.");

    internal bool IsShuttingDown => shuttingDown;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = args;
        instanceMutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            instanceMutex.Dispose();
            instanceMutex = null;
            Exit();
            return;
        }

        controller = new PowerMouseController();
        mainWindow = new MainWindow();

        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
        trayIcon = new TrayIconService(windowHandle)
        {
            IsEnabledProvider = () => Controller.IsEnabled,
        };
        cursorIndicator = new CursorIndicatorService(mainWindow.DispatcherQueue);

        trayIcon.OpenRequested += mainWindow.ShowSettings;
        trayIcon.ToggleRequested += Controller.Toggle;
        trayIcon.ExitRequested += Shutdown;
        Controller.EnabledChanged += OnEnabledChanged;
        Controller.PreferencesChanged += UpdatePointerIndicator;
        Controller.Error += OnControllerError;

        try
        {
            clickHints = new ClickHintsService(Controller.Settings, mainWindow.DispatcherQueue);
            clickHints.Notice += OnClickHintsNotice;
            Controller.SetKeyInterceptor(clickHints.ProcessKey);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            trayIcon.ShowNotification("Mouse Hints is unavailable", exception.Message);
        }

        Controller.Start();
        mainWindow.SetEnabledStatus(Controller.IsEnabled);
        UpdatePointerIndicator();

        var showSettings = Environment.GetCommandLineArgs().Any(argument =>
            string.Equals(argument, "--show-settings", StringComparison.OrdinalIgnoreCase));
        if (showSettings)
        {
            mainWindow.ShowSettings();
        }
    }

    internal void Shutdown()
    {
        if (shuttingDown)
        {
            return;
        }

        shuttingDown = true;
        Dispose();
        mainWindow?.Close();
        Exit();
    }

    public void Dispose()
    {
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

    private void OnEnabledChanged(bool enabled)
    {
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

    private void OnControllerError(Exception exception)
    {
        mainWindow?.DispatcherQueue.TryEnqueue(() =>
            trayIcon?.ShowNotification("PowerMouse encountered a problem", exception.Message));
    }

    private void OnClickHintsNotice(string message)
    {
        mainWindow?.DispatcherQueue.TryEnqueue(() =>
            trayIcon?.ShowNotification("Mouse Hints", message));
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        _ = sender;
        args.Handled = true;
        trayIcon?.ShowNotification("PowerMouse encountered a problem", args.Message);
    }
}
