using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Hints;

public static class HintLayoutEngine
{
    public static IReadOnlyList<PositionedHintLayoutItem> Layout(
        IReadOnlyList<HintLayoutItem> items,
        ElementBounds container,
        double gap = 2)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (container.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(container));
        }

        if (!double.IsFinite(gap) || gap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gap));
        }

        var placed = new List<PositionedHintLayoutItem>(items.Count);
        foreach (var item in items.OrderBy(value => value.PreferredY).ThenBy(value => value.PreferredX))
        {
            Validate(item, container);
            var anchorX = Math.Clamp(item.PreferredX, container.X, container.Right - item.Width);
            var anchorY = Math.Clamp(item.PreferredY, container.Y, container.Bottom - item.Height);
            var candidates = GetAxisCandidates(anchorX, anchorY, item, placed, container, gap);
            var selected = FindNearestFree(candidates, anchorX, anchorY, item, placed, gap) ??
                           FindScanlinePosition(anchorX, anchorY, item, placed, container, gap);
            placed.Add(new PositionedHintLayoutItem(item.Index, selected));
        }

        return placed.OrderBy(item => item.Index).ToArray();
    }

    private static IEnumerable<(double X, double Y)> GetAxisCandidates(
        double anchorX,
        double anchorY,
        HintLayoutItem item,
        IReadOnlyList<PositionedHintLayoutItem> placed,
        ElementBounds container,
        double gap)
    {
        yield return (anchorX, anchorY);
        foreach (var existing in placed)
        {
            yield return (anchorX, ClampY(existing.Bounds.Bottom + gap, item.Height, container));
            yield return (anchorX, ClampY(existing.Bounds.Y - item.Height - gap, item.Height, container));
            yield return (ClampX(existing.Bounds.Right + gap, item.Width, container), anchorY);
            yield return (ClampX(existing.Bounds.X - item.Width - gap, item.Width, container), anchorY);
        }
    }

    private static ElementBounds? FindNearestFree(
        IEnumerable<(double X, double Y)> candidates,
        double anchorX,
        double anchorY,
        HintLayoutItem item,
        IReadOnlyList<PositionedHintLayoutItem> placed,
        double gap) =>
        candidates
            .Distinct()
            .Select(candidate => new ElementBounds(candidate.X, candidate.Y, item.Width, item.Height))
            .Where(candidate => IsFree(candidate, placed, gap))
            .OrderBy(candidate => DistanceSquared(candidate.X, candidate.Y, anchorX, anchorY))
            .ThenBy(candidate => candidate.Y)
            .ThenBy(candidate => candidate.X)
            .Cast<ElementBounds?>()
            .FirstOrDefault();

    private static ElementBounds FindScanlinePosition(
        double anchorX,
        double anchorY,
        HintLayoutItem item,
        IReadOnlyList<PositionedHintLayoutItem> placed,
        ElementBounds container,
        double gap)
    {
        var xPositions = new[] { anchorX, container.X }
            .Concat(placed.SelectMany(existing => new[]
            {
                ClampX(existing.Bounds.Right + gap, item.Width, container),
                ClampX(existing.Bounds.X - item.Width - gap, item.Width, container),
            }))
            .Distinct()
            .OrderBy(x => Math.Abs(x - anchorX))
            .ToArray();
        var yPositions = new[] { anchorY, container.Y }
            .Concat(placed.SelectMany(existing => new[]
            {
                ClampY(existing.Bounds.Bottom + gap, item.Height, container),
                ClampY(existing.Bounds.Y - item.Height - gap, item.Height, container),
            }))
            .Distinct()
            .OrderBy(y => Math.Abs(y - anchorY))
            .ToArray();

        ElementBounds? best = null;
        var bestDistance = double.MaxValue;
        foreach (var y in yPositions)
        {
            foreach (var x in xPositions)
            {
                var distance = DistanceSquared(x, y, anchorX, anchorY);
                if (distance >= bestDistance)
                {
                    continue;
                }

                var candidate = new ElementBounds(x, y, item.Width, item.Height);
                if (IsFree(candidate, placed, gap))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
        }

        return best ?? throw new InvalidOperationException("The hint labels do not fit inside the available desktop area.");
    }

    private static bool IsFree(
        ElementBounds candidate,
        IReadOnlyList<PositionedHintLayoutItem> placed,
        double gap) =>
        placed.All(existing => !Overlaps(candidate, existing.Bounds, gap));

    private static bool Overlaps(ElementBounds left, ElementBounds right, double gap) =>
        left.X < right.Right + gap &&
        left.Right + gap > right.X &&
        left.Y < right.Bottom + gap &&
        left.Bottom + gap > right.Y;

    private static double ClampX(double x, double width, ElementBounds container) =>
        Math.Clamp(x, container.X, container.Right - width);

    private static double ClampY(double y, double height, ElementBounds container) =>
        Math.Clamp(y, container.Y, container.Bottom - height);

    private static double DistanceSquared(double x, double y, double anchorX, double anchorY)
    {
        var deltaX = x - anchorX;
        var deltaY = y - anchorY;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private static void Validate(HintLayoutItem item, ElementBounds container)
    {
        if (!double.IsFinite(item.PreferredX) ||
            !double.IsFinite(item.PreferredY) ||
            !double.IsFinite(item.Width) ||
            !double.IsFinite(item.Height) ||
            item.Width <= 0 ||
            item.Height <= 0 ||
            item.Width > container.Width ||
            item.Height > container.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(item), "Hint dimensions must fit inside the layout container.");
        }
    }
}
