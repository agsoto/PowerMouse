using PowerMouse.Core.Engine;
using PowerMouse.Core.Models;

namespace PowerMouse.App.Services;

internal sealed class PowerMouseRuntime : IDisposable
{
    private readonly KeyboardHookService keyboardHook = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly InputEngine engine;
    private Task? movementTask;
    private PowerMouseProfile activeProfile;
    private bool disposed;

    public PowerMouseRuntime(PowerMouseProfile profile, bool enableOnStartup)
    {
        activeProfile = profile.Snapshot();
        var initiallyEnabled = enableOnStartup ||
                               (profile.ActivationMode == ActivationMode.LockKey && MouseInputService.IsLockKeyOn(profile.ActivationKey));

        engine = new InputEngine(activeProfile, initiallyEnabled);
        engine.EnabledChanged += status => EnabledChanged?.Invoke(status);
        keyboardHook.KeyReceived = ProcessKey;

        if (enableOnStartup && profile.ActivationMode == ActivationMode.LockKey)
        {
            MouseInputService.SetLockKeyState(profile.ActivationKey, enabled: true);
        }
    }

    public event Action<bool>? EnabledChanged;

    public event Action<Exception>? InputError;

    public Func<KeyboardStroke, bool>? KeyInterceptor { get; set; }

    public bool IsEnabled => engine.IsEnabled;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        keyboardHook.Start();
        movementTask ??= Task.Run(() => RunMovementLoopAsync(cancellation.Token));
    }

    public void ApplyProfile(PowerMouseProfile profile)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var previousProfile = activeProfile;
        activeProfile = profile.Snapshot();
        SendSafely(engine.UpdateProfile(activeProfile));

        if (previousProfile.ActivationMode == ActivationMode.LockKey &&
            (activeProfile.ActivationMode != ActivationMode.LockKey ||
             previousProfile.ActivationKey != activeProfile.ActivationKey))
        {
            TryInput(() => MouseInputService.SetLockKeyState(previousProfile.ActivationKey, enabled: false));
        }

        if (activeProfile.ActivationMode == ActivationMode.LockKey)
        {
            TryInput(() => MouseInputService.SetLockKeyState(activeProfile.ActivationKey, engine.IsEnabled));
        }
    }

    public void SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (activeProfile.ActivationMode == ActivationMode.LockKey)
        {
            TryInput(() => MouseInputService.SetLockKeyState(activeProfile.ActivationKey, enabled));
        }

        SendSafely(engine.SetEnabled(enabled));
    }

    public void Toggle() => SetEnabled(!engine.IsEnabled);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        cancellation.Cancel();
        keyboardHook.Dispose();
        SendSafely(engine.Reset());

        try
        {
            movementTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Cancellation during shutdown is expected.
        }

        cancellation.Dispose();
    }

    private bool ProcessKey(KeyboardStroke stroke)
    {
        if (KeyInterceptor?.Invoke(stroke) == true)
        {
            return true;
        }

        var result = engine.Process(stroke);
        SendSafely(result.Commands);
        return result.Suppress;
    }

    private async Task RunMovementLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(33));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var movement = engine.GetMovement();
                if (!movement.IsZero)
                {
                    TryInput(() => MouseInputService.Move(movement.X, movement.Y));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private void SendSafely(IReadOnlyList<MouseCommand> commands) => TryInput(() => MouseInputService.Send(commands));

    private void TryInput(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            InputError?.Invoke(exception);
        }
    }
}
