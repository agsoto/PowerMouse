using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Hints;

public sealed record HintTargetPlacement(DetectedElement Element, ElementBounds ClickTarget);
