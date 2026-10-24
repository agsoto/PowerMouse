using PowerMouse.App.Diagnostics;
using PowerMouse.Core;
using PowerMouse.Core.Engine;
using PowerMouse.Core.Models;

namespace PowerMouse.App.Services;

internal sealed class PowerMouseController : IDisposable
{
    private readonly SettingsStore settingsStore;
    private readonly PowerMouseRuntime runtime;
    private bool disposed;

    public PowerMouseController()
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PowerMouse");
        settingsStore = new SettingsStore(Path.Combine(settingsDirectory, "settings.json"));
        Settings = settingsStore.Load();
        Logger.LogInfo($"Settings loaded from {settingsStore.FilePath}. Profiles={Settings.Profiles.Count}.");
        runtime = new PowerMouseRuntime(Settings.ActiveProfile, Settings.EnabledOnStartup);
        runtime.EnabledChanged += enabled => EnabledChanged?.Invoke(enabled);
        runtime.InputError += exception => Error?.Invoke(exception);

        TryPersistStartupRegistration();
    }

    public event Action<bool>? EnabledChanged;

    public event Action? PreferencesChanged;

    public event Action<Exception>? Error;

    public PowerMouseSettings Settings { get; }

    public bool IsEnabled => runtime.IsEnabled;

    public void Start()
    {
        runtime.Start();
        Logger.LogInfo("Keyboard hook and movement runtime started.");
    }

    public void SetEnabled(bool enabled) => runtime.SetEnabled(enabled);

    public void Toggle() => runtime.Toggle();

    public void SetKeyInterceptor(Func<KeyboardStroke, bool>? interceptor) => runtime.KeyInterceptor = interceptor;

    public void SaveGeneralSettings()
    {
        SaveSettings();
        Logger.LogInfo(
            $"General settings saved. MouseHintsEnabled={Settings.MouseHintsEnabled}; " +
            $"StartWithWindows={Settings.StartWithWindows}; EnabledOnStartup={Settings.EnabledOnStartup}.");
        PreferencesChanged?.Invoke();
    }

    public void SaveStartupSettings(bool startWithWindows, bool alwaysRunAsAdministrator)
    {
        var oldStartup = Settings.StartWithWindows;
        var oldAdministrator = Settings.AlwaysRunAsAdministrator;
        if (oldAdministrator != alwaysRunAsAdministrator && !AdministratorRestartService.IsAdministrator)
            throw new InvalidOperationException("Restart PowerMouse as administrator before changing this preference.");
        try
        {
            StartupService.SetEnabled(startWithWindows, alwaysRunAsAdministrator);
            Settings.StartWithWindows = startWithWindows;
            Settings.AlwaysRunAsAdministrator = alwaysRunAsAdministrator;
            settingsStore.Save(Settings);
        }
        catch (Exception saveError) when (saveError is not StackOverflowException and not OutOfMemoryException)
        {
            Settings.StartWithWindows = oldStartup;
            Settings.AlwaysRunAsAdministrator = oldAdministrator;
            try { StartupService.SetEnabled(oldStartup, oldAdministrator); }
            catch (Exception rollbackError) when (rollbackError is not StackOverflowException and not OutOfMemoryException)
            {
                throw new AggregateException("The settings could not be saved or startup restored. Restart as administrator and reapply your startup preferences.", saveError, rollbackError);
            }
            throw;
        }
        Logger.LogInfo($"Startup settings saved. StartWithWindows={startWithWindows}; AlwaysRunAsAdministrator={alwaysRunAsAdministrator}.");
        PreferencesChanged?.Invoke();
    }

    public void SaveProfileSettings()
    {
        Settings.Normalize();
        SaveSettings();
        runtime.ApplyProfile(Settings.ActiveProfile);
        Logger.LogDebug($"Profile settings saved for '{Settings.ActiveProfile.Name}'.");
        PreferencesChanged?.Invoke();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        runtime.Dispose();
        Logger.LogInfo("Keyboard hook and movement runtime stopped.");
    }

    private void SaveSettings()
    {
        try
        {
            settingsStore.Save(Settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Error?.Invoke(exception);
        }
    }

    private void TryPersistStartupRegistration()
    {
        // The administrator replacement will reconcile the task. A canceled UAC
        // prompt must not downgrade an existing elevated startup registration.
        if (Settings.AlwaysRunAsAdministrator && !AdministratorRestartService.IsAdministrator) return;
        try
        {
            StartupService.SetEnabled(Settings.StartWithWindows, Settings.AlwaysRunAsAdministrator);
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            Logger.LogError("Startup registration could not be updated.", exception);
            Error?.Invoke(exception);
        }
    }
}
