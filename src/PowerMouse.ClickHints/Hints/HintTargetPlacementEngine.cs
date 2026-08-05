using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Hints;

public static class HintTargetPlacementEngine
{
    private const double PreferredSeparationSquared = 18 * 18;
    private static readonly double[] GridFractions = [0.5, 0.25, 0.75, 0.1, 0.9];

    public static IReadOnlyList<HintTargetPlacement> Place(IReadOnlyList<DetectedElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        if (elements.Count == 0)
        {
            return [];
        }

        var visualCenterX = elements.Average(element => CenterX(element.VisualBounds));
        var visualCenterY = elements.Average(element => CenterY(element.VisualBounds));
        var placed = new List<(int Index, HintTargetPlacement Placement)>(elements.Count);

        // Smaller targets are less flexible, so reserve their useful area first.
        foreach (var item in elements
                     .Select((element, index) => (Element: element, Index: index))
                     .OrderBy(item => item.Element.Bounds.Width * item.Element.Bounds.Height)
                     .ThenBy(item => item.Index))
        {
            var candidates = GetCandidates(item.Element.Bounds);
            var preferred = GetPreferredPoint(item.Element, visualCenterX, visualCenterY);
            var selected = candidates
                .Select(candidate => new
                {
                    Point = candidate,
                    Separation = placed.Count == 0
                        ? PreferredSeparationSquared
                        : Math.Min(
                            PreferredSeparationSquared,
                            placed.Min(existing => DistanceSquared(candidate, GetCenter(existing.Placement.ClickTarget)))),
                    PreferredDistance = DistanceSquared(candidate, preferred),
                })
                .OrderByDescending(candidate => candidate.Separation)
                .ThenBy(candidate => candidate.PreferredDistance)
                .First()
                .Point;

            var targetSize = Math.Min(2, Math.Min(item.Element.Bounds.Width, item.Element.Bounds.Height) / 2);
            placed.Add((
                item.Index,
                new HintTargetPlacement(
                    item.Element,
                    new ElementBounds(
                        selected.X - (targetSize / 2),
                        selected.Y - (targetSize / 2),
                        targetSize,
                        targetSize))));
        }

        return placed
            .OrderBy(item => item.Index)
            .Select(item => item.Placement)
            .ToArray();
    }

    private static (double X, double Y)[] GetCandidates(ElementBounds bounds)
    {
        var insetX = Math.Min(4, bounds.Width / 4);
        var insetY = Math.Min(4, bounds.Height / 4);
        var usableWidth = Math.Max(0, bounds.Width - (insetX * 2));
        var usableHeight = Math.Max(0, bounds.Height - (insetY * 2));
        return GridFractions
            .SelectMany(y => GridFractions.Select(x => (
                X: bounds.X + insetX + (usableWidth * x),
                Y: bounds.Y + insetY + (usableHeight * y))))
            .Distinct()
            .ToArray();
    }

    private static (double X, double Y) GetPreferredPoint(
        DetectedElement element,
        double clusterVisualCenterX,
        double clusterVisualCenterY)
    {
        var bounds = element.Bounds;
        var offsetX = Math.Clamp(
            CenterX(element.VisualBounds) - clusterVisualCenterX,
            -bounds.Width / 4,
            bounds.Width / 4);
        var offsetY = Math.Clamp(
            CenterY(element.VisualBounds) - clusterVisualCenterY,
            -bounds.Height / 4,
            bounds.Height / 4);
        return (CenterX(bounds) + offsetX, CenterY(bounds) + offsetY);
    }

    private static (double X, double Y) GetCenter(ElementBounds bounds) =>
        (CenterX(bounds), CenterY(bounds));

    private static double CenterX(ElementBounds bounds) => bounds.X + (bounds.Width / 2);

    private static double CenterY(ElementBounds bounds) => bounds.Y + (bounds.Height / 2);

    private static double DistanceSquared((double X, double Y) left, (double X, double Y) right)
    {
        var deltaX = left.X - right.X;
        var deltaY = left.Y - right.Y;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }
}
