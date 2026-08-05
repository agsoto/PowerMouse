namespace PowerMouse.ClickHints.Detection;

public sealed record RecognizedTextRegion(
    string Text,
    ElementBounds Bounds,
    double Confidence = 0.5);
