namespace PowerMouse.ClickHints.Hints;

public readonly record struct HintLayoutItem(
    int Index,
    double PreferredX,
    double PreferredY,
    double Width,
    double Height);
