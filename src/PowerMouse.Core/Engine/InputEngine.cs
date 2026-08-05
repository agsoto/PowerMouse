using PowerMouse.Core.Models;

namespace PowerMouse.Core.Engine;

public sealed class InputEngine
{
    private static readonly IReadOnlyList<MouseCommand> NoCommands = Array.Empty<MouseCommand>();

    private readonly Lock stateLock = new();
    private readonly HashSet<int> downKeys = [];
    private readonly HashSet<int> suppressedUntilKeyUp = [];
    private readonly HashSet<InputAction> heldMovement = [];
    private readonly HashSet<InputAction> heldButtons = [];
    private PowerMouseProfile profile;
    private bool enabled;

    public InputEngine(PowerMouseProfile profile, bool enabled = false)
    {
        ArgumentNullException.ThrowIfNull(profile);
        this.profile = profile.Snapshot();
        this.enabled = enabled;
    }

    public event Action<bool>? EnabledChanged;

    public bool IsEnabled
    {
        get
        {
            lock (stateLock)
            {
                return enabled;
            }
        }
    }

    public InputResult Process(KeyboardStroke stroke)
    {
        if (stroke.IsInjected)
        {
            return InputResult.Pass;
        }

        var key = KeyNormalizer.Normalize(stroke);
        bool? statusChange = null;
        InputResult result;

        lock (stateLock)
        {
            var wasDown = downKeys.Contains(key);
            if (stroke.IsKeyDown)
            {
                downKeys.Add(key);
            }
            else
            {
                downKeys.Remove(key);
            }

            var wasSuppressed = !stroke.IsKeyDown && suppressedUntilKeyUp.Remove(key);

            if (IsActivationGestureLocked(key, stroke.IsKeyDown, wasDown))
            {
                var releaseCommands = SetEnabledLocked(!enabled);
                statusChange = enabled;
                if (stroke.IsKeyDown)
                {
                    suppressedUntilKeyUp.Add(key);
                }

                result = new InputResult(true, releaseCommands);
            }
            else if (profile.ActivationMode == ActivationMode.LockKey &&
                     key == profile.ActivationKey &&
                     stroke.IsKeyDown &&
                     !wasDown)
            {
                var releaseCommands = SetEnabledLocked(!enabled);
                statusChange = enabled;

                // Let the lock key reach Windows so its LED and system state remain
                // consistent with the application's activation state.
                result = new InputResult(false, releaseCommands);
            }
            else if (!stroke.IsKeyDown && !wasSuppressed)
            {
                // If the key-down was forwarded, its key-up must also be forwarded.
                // Swallowing only the release leaves downstream keyboard hooks with
                // a phantom held key when activation changes mid-press.
                result = InputResult.Pass;
            }
            else if (!enabled)
            {
                result = wasSuppressed ? InputResult.Suppressed : InputResult.Pass;
            }
            else
            {
                result = ProcessEnabledKeyLocked(key, stroke.IsKeyDown, wasDown, wasSuppressed);
            }
        }

        if (statusChange is bool newStatus)
        {
            EnabledChanged?.Invoke(newStatus);
        }

        return result;
    }

    public IReadOnlyList<MouseCommand> SetEnabled(bool value)
    {
        bool changed;
        IReadOnlyList<MouseCommand> commands;

        lock (stateLock)
        {
            changed = enabled != value;
            commands = SetEnabledLocked(value);
        }

        if (changed)
        {
            EnabledChanged?.Invoke(value);
        }

        return commands;
    }

    public IReadOnlyList<MouseCommand> Toggle()
    {
        bool newValue;
        IReadOnlyList<MouseCommand> commands;

        lock (stateLock)
        {
            newValue = !enabled;
            commands = SetEnabledLocked(newValue);
        }

        EnabledChanged?.Invoke(newValue);
        return commands;
    }

    public IReadOnlyList<MouseCommand> UpdateProfile(PowerMouseProfile value)
    {
        ArgumentNullException.ThrowIfNull(value);

        lock (stateLock)
        {
            var commands = ReleaseHeldInputsLocked();
            downKeys.Clear();
            profile = value.Snapshot();
            return commands;
        }
    }

    public MovementVector GetMovement()
    {
        lock (stateLock)
        {
            if (!enabled || heldMovement.Count == 0)
            {
                return default;
            }

            var x = 0;
            var y = 0;

            if (heldMovement.Contains(InputAction.MoveLeft) ||
                heldMovement.Contains(InputAction.MoveUpLeft) ||
                heldMovement.Contains(InputAction.MoveDownLeft))
            {
                x--;
            }

            if (heldMovement.Contains(InputAction.MoveRight) ||
                heldMovement.Contains(InputAction.MoveUpRight) ||
                heldMovement.Contains(InputAction.MoveDownRight))
            {
                x++;
            }

            if (heldMovement.Contains(InputAction.MoveUp) ||
                heldMovement.Contains(InputAction.MoveUpLeft) ||
                heldMovement.Contains(InputAction.MoveUpRight))
            {
                y--;
            }

            if (heldMovement.Contains(InputAction.MoveDown) ||
                heldMovement.Contains(InputAction.MoveDownLeft) ||
                heldMovement.Contains(InputAction.MoveDownRight))
            {
                y++;
            }

            var speed = IsAlternateSpeedHeldLocked() ? profile.AlternateSpeed : profile.MovementSpeed;
            return new MovementVector(x * speed, y * speed);
        }
    }

    public IReadOnlyList<MouseCommand> Reset()
    {
        lock (stateLock)
        {
            var commands = ReleaseHeldInputsLocked();
            downKeys.Clear();
            suppressedUntilKeyUp.Clear();
            return commands;
        }
    }

    private InputResult ProcessEnabledKeyLocked(int key, bool isKeyDown, bool wasDown, bool wasSuppressed)
    {
        var binding = profile.Bindings.FirstOrDefault(pair => pair.Value == key && pair.Value != VirtualKeys.None);
        if (binding.Value == VirtualKeys.None)
        {
            return wasSuppressed ? InputResult.Suppressed : InputResult.Pass;
        }

        var action = binding.Key;
        if (IsMovementAction(action))
        {
            if (isKeyDown)
            {
                heldMovement.Add(action);
                suppressedUntilKeyUp.Add(key);
            }
            else
            {
                heldMovement.Remove(action);
            }

            return InputResult.Suppressed;
        }

        if (action is InputAction.ScrollUp or InputAction.ScrollDown)
        {
            if (isKeyDown)
            {
                suppressedUntilKeyUp.Add(key);
                var direction = action == InputAction.ScrollUp ? 120 : -120;
                return new InputResult(true, [new MouseCommand(MouseCommandKind.Wheel, direction)]);
            }

            return InputResult.Suppressed;
        }

        if (isKeyDown)
        {
            suppressedUntilKeyUp.Add(key);
            if (wasDown || !heldButtons.Add(action))
            {
                return InputResult.Suppressed;
            }

            return new InputResult(true, [new MouseCommand(GetButtonCommand(action, isDown: true))]);
        }

        if (heldButtons.Remove(action))
        {
            return new InputResult(true, [new MouseCommand(GetButtonCommand(action, isDown: false))]);
        }

        return InputResult.Suppressed;
    }

    private bool IsActivationGestureLocked(int key, bool isKeyDown, bool wasDown)
    {
        if (profile.ActivationMode != ActivationMode.Shortcut || !isKeyDown || wasDown)
        {
            return false;
        }

        var shortcut = profile.ActivationShortcut;
        return key == shortcut.VirtualKey &&
               (GetCurrentModifiersLocked() & shortcut.Modifiers) == shortcut.Modifiers;
    }

    private HotkeyModifiers GetCurrentModifiersLocked()
    {
        var modifiers = HotkeyModifiers.None;
        if (downKeys.Overlaps([VirtualKeys.Control, VirtualKeys.LeftControl, VirtualKeys.RightControl]))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (downKeys.Overlaps([VirtualKeys.Menu, VirtualKeys.LeftMenu, VirtualKeys.RightMenu]))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (downKeys.Overlaps([VirtualKeys.Shift, VirtualKeys.LeftShift, VirtualKeys.RightShift]))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (downKeys.Overlaps([VirtualKeys.LeftWindows, VirtualKeys.RightWindows]))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        return modifiers;
    }

    private bool IsAlternateSpeedHeldLocked() =>
        profile.AlternateSpeedKey != VirtualKeys.None && downKeys.Contains(profile.AlternateSpeedKey);

    private IReadOnlyList<MouseCommand> SetEnabledLocked(bool value)
    {
        if (enabled == value)
        {
            return NoCommands;
        }

        enabled = value;
        return value ? NoCommands : ReleaseHeldInputsLocked();
    }

    private List<MouseCommand> ReleaseHeldInputsLocked()
    {
        var commands = new List<MouseCommand>(heldButtons.Count);
        foreach (var action in heldButtons)
        {
            commands.Add(new MouseCommand(GetButtonCommand(action, isDown: false)));
        }

        heldButtons.Clear();
        heldMovement.Clear();
        return commands;
    }

    private static bool IsMovementAction(InputAction action) => action is
        InputAction.MoveUp or InputAction.MoveDown or InputAction.MoveLeft or InputAction.MoveRight or
        InputAction.MoveUpLeft or InputAction.MoveUpRight or InputAction.MoveDownLeft or InputAction.MoveDownRight;

    private static MouseCommandKind GetButtonCommand(InputAction action, bool isDown) => (action, isDown) switch
    {
        (InputAction.LeftButton, true) => MouseCommandKind.LeftButtonDown,
        (InputAction.LeftButton, false) => MouseCommandKind.LeftButtonUp,
        (InputAction.RightButton, true) => MouseCommandKind.RightButtonDown,
        (InputAction.RightButton, false) => MouseCommandKind.RightButtonUp,
        (InputAction.MiddleButton, true) => MouseCommandKind.MiddleButtonDown,
        (InputAction.MiddleButton, false) => MouseCommandKind.MiddleButtonUp,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Action is not a mouse button."),
    };
}
