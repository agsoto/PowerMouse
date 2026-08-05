namespace PowerMouse.Core.Models;

public sealed record HotkeyGesture
{
    public HotkeyModifiers Modifiers { get; init; } = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    public int VirtualKey { get; init; } = VirtualKeys.F10;

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(VirtualKeyCatalog.GetName(VirtualKey));
        return string.Join(" + ", parts);
    }
}

