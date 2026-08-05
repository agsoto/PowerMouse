using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Hints;

public sealed record HintCluster(IReadOnlyList<DetectedElement> Elements)
{
    public bool IsCluster => Elements.Count > 1;

    public double CenterX => Elements.Average(element => element.Bounds.X + (element.Bounds.Width / 2));

    public double CenterY => Elements.Average(element => element.Bounds.Y + (element.Bounds.Height / 2));
}
