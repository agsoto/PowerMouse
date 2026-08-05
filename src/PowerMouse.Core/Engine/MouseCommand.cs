namespace PowerMouse.Core.Engine;

public enum MouseCommandKind
{
    LeftButtonDown,
    LeftButtonUp,
    RightButtonDown,
    RightButtonUp,
    MiddleButtonDown,
    MiddleButtonUp,
    Wheel,
}

public readonly record struct MouseCommand(MouseCommandKind Kind, int Data = 0);

