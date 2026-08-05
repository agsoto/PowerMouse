namespace PowerMouse.ClickHints.Detection;

public sealed record DetectionResult(
    IReadOnlyList<DetectedElement> Elements,
    bool UsedOcrFallback,
    TimeSpan Elapsed,
    IReadOnlyList<string> Diagnostics);
