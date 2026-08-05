namespace PowerMouse.Core.Models;

public sealed class PowerMouseProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Default";

    public ActivationMode ActivationMode { get; set; } = ActivationMode.LockKey;

    public int ActivationKey { get; set; } = VirtualKeys.ScrollLock;

    public HotkeyGesture ActivationShortcut { get; set; } = new();

    public int MovementSpeed { get; set; } = 20;

    public int AlternateSpeed { get; set; } = 1;

    public int AlternateSpeedKey { get; set; } = VirtualKeys.LeftShift;

    public Dictionary<InputAction, int> Bindings { get; set; } = CreateDefaultBindings();

    public PowerMouseProfile Snapshot() => new()
    {
        Id = Id,
        Name = Name,
        ActivationMode = ActivationMode,
        ActivationKey = ActivationKey,
        ActivationShortcut = ActivationShortcut with { },
        MovementSpeed = MovementSpeed,
        AlternateSpeed = AlternateSpeed,
        AlternateSpeedKey = AlternateSpeedKey,
        Bindings = new Dictionary<InputAction, int>(Bindings),
    };

    public PowerMouseProfile Clone(string? name = null)
    {
        var clone = Snapshot();
        clone.Id = Guid.NewGuid();
        clone.Name = name ?? $"{Name} copy";
        return clone;
    }

    public void Normalize()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? "Profile" : Name.Trim();
        ActivationShortcut ??= new HotkeyGesture();
        Bindings ??= CreateDefaultBindings();
        MovementSpeed = Math.Clamp(MovementSpeed, 1, 50);
        AlternateSpeed = Math.Clamp(AlternateSpeed, 1, 500);

        foreach (var action in Enum.GetValues<InputAction>())
        {
            Bindings.TryAdd(action, VirtualKeys.None);
        }
    }

    public static Dictionary<InputAction, int> CreateDefaultBindings() => new()
    {
        [InputAction.MoveUp] = VirtualKeys.Numpad8,
        [InputAction.MoveDown] = VirtualKeys.Numpad2,
        [InputAction.MoveLeft] = VirtualKeys.Numpad4,
        [InputAction.MoveRight] = VirtualKeys.Numpad6,
        [InputAction.MoveUpLeft] = VirtualKeys.Numpad7,
        [InputAction.MoveUpRight] = VirtualKeys.Numpad9,
        [InputAction.MoveDownLeft] = VirtualKeys.Numpad1,
        [InputAction.MoveDownRight] = VirtualKeys.Numpad3,
        [InputAction.LeftButton] = VirtualKeys.Numpad0,
        [InputAction.RightButton] = VirtualKeys.NumpadEnter,
        [InputAction.MiddleButton] = VirtualKeys.Numpad5,
        [InputAction.ScrollUp] = VirtualKeys.Divide,
        [InputAction.ScrollDown] = VirtualKeys.Multiply,
    };
}
