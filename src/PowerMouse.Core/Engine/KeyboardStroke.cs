namespace PowerMouse.Core.Engine;

public readonly record struct KeyboardStroke(
    int VirtualKey,
    int ScanCode,
    bool IsExtended,
    bool IsKeyDown,
    bool IsInjected = false);

