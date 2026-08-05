namespace PowerMouse.ClickHints.Detection;

public sealed record UiaElementSnapshot(
    string Id,
    string? Name,
    ElementBounds Bounds,
    UiaControlType ControlType,
    bool IsEnabled,
    bool IsOffscreen,
    bool IsKeyboardFocusable,
    bool SupportsInvoke,
    bool SupportsToggle,
    bool SupportsSelectionItem,
    bool SupportsExpandCollapse,
    bool SupportsValue,
    bool SupportsRangeValue);
