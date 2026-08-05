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

    public void Start() => runtime.Start();

    public void SetEnabled(bool enabled) => runtime.SetEnabled(enabled);

    public void Toggle() => runtime.Toggle();

    public void SetKeyInterceptor(Func<KeyboardStroke, bool>? interceptor) => runtime.KeyInterceptor = interceptor;

    public void SaveGeneralSettings()
    {
        SaveSettings();
        TryPersistStartupRegistration();
        PreferencesChanged?.Invoke();
    }

    public void SaveProfileSettings()
    {
        Settings.Normalize();
        SaveSettings();
        runtime.ApplyProfile(Settings.ActiveProfile);
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
        try
        {
            StartupService.SetEnabled(Settings.StartWithWindows);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException)
        {
            Error?.Invoke(exception);
        }
    }
}
