using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Hints;

public sealed record ClickHint(
    string Keys,
    DetectedElement Element,
    int ClusterSize = 1,
    ElementBounds? LeaderTarget = null);
