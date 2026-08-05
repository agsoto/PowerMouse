using System.Collections.ObjectModel;

namespace PowerMouse.Core.Models;

public static class VirtualKeyCatalog
{
    public static ReadOnlyCollection<KeyOption> BindableKeys { get; } = BuildBindableKeys().AsReadOnly();

    public static ReadOnlyCollection<KeyOption> LockKeys { get; } = new List<KeyOption>
    {
        new(VirtualKeys.ScrollLock, "Scroll Lock"),
        new(VirtualKeys.NumLock, "Num Lock"),
        new(VirtualKeys.Capital, "Caps Lock"),
    }.AsReadOnly();

    private static readonly Dictionary<int, string> Names = BuildNames();

    public static string GetName(int virtualKey) => Names.TryGetValue(virtualKey, out var name)
        ? name
        : $"Key 0x{virtualKey:X2}";

    public static bool IsModifier(int virtualKey) => virtualKey is
        VirtualKeys.Shift or VirtualKeys.LeftShift or VirtualKeys.RightShift or
        VirtualKeys.Control or VirtualKeys.LeftControl or VirtualKeys.RightControl or
        VirtualKeys.Menu or VirtualKeys.LeftMenu or VirtualKeys.RightMenu or
        VirtualKeys.LeftWindows or VirtualKeys.RightWindows;

    private static List<KeyOption> BuildBindableKeys()
    {
        var keys = new List<KeyOption> { new(VirtualKeys.None, "Unassigned") };
        keys.AddRange(Enumerable.Range(VirtualKeys.Key0, 10).Select(key => new KeyOption(key, ((char)key).ToString())));
        keys.AddRange(Enumerable.Range(VirtualKeys.KeyA, 26).Select(key => new KeyOption(key, ((char)key).ToString())));
        keys.AddRange(Enumerable.Range(VirtualKeys.F1, 12).Select(key => new KeyOption(key, $"F{key - VirtualKeys.F1 + 1}")));
        keys.AddRange(Enumerable.Range(VirtualKeys.Numpad0, 10).Select(key => new KeyOption(key, $"Numpad {key - VirtualKeys.Numpad0}")));
        keys.AddRange(
        [
            new(VirtualKeys.NumpadEnter, "Numpad Enter"),
            new(VirtualKeys.Add, "Numpad +"),
            new(VirtualKeys.Subtract, "Numpad −"),
            new(VirtualKeys.Multiply, "Numpad ×"),
            new(VirtualKeys.Divide, "Numpad ÷"),
            new(VirtualKeys.NumpadDecimal, "Numpad decimal"),
            new(VirtualKeys.Space, "Space"),
            new(VirtualKeys.Tab, "Tab"),
            new(VirtualKeys.Back, "Backspace"),
            new(VirtualKeys.Return, "Enter"),
            new(VirtualKeys.Left, "Left arrow"),
            new(VirtualKeys.Right, "Right arrow"),
            new(VirtualKeys.Up, "Up arrow"),
            new(VirtualKeys.Down, "Down arrow"),
            new(VirtualKeys.Home, "Home"),
            new(VirtualKeys.End, "End"),
            new(VirtualKeys.Prior, "Page Up"),
            new(VirtualKeys.Next, "Page Down"),
            new(VirtualKeys.Insert, "Insert"),
            new(VirtualKeys.Delete, "Delete"),
            new(VirtualKeys.LeftShift, "Left Shift"),
            new(VirtualKeys.RightShift, "Right Shift"),
            new(VirtualKeys.LeftControl, "Left Ctrl"),
            new(VirtualKeys.RightControl, "Right Ctrl"),
            new(VirtualKeys.LeftMenu, "Left Alt"),
            new(VirtualKeys.RightMenu, "Right Alt"),
        ]);

        return keys;
    }

    private static Dictionary<int, string> BuildNames()
    {
        var names = BindableKeys.ToDictionary(option => option.Code, option => option.Name);
        foreach (var option in LockKeys)
        {
            names[option.Code] = option.Name;
        }

        names[VirtualKeys.Escape] = "Escape";
        names[VirtualKeys.LeftWindows] = "Left Win";
        names[VirtualKeys.RightWindows] = "Right Win";
        return names;
    }
}
