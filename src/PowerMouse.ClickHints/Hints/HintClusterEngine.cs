using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Hints;

public static class HintClusterEngine
{
    public static IReadOnlyList<HintCluster> Cluster(
        IReadOnlyList<DetectedElement> elements,
        double horizontalDistance = 48,
        double verticalDistance = 30)
    {
        ArgumentNullException.ThrowIfNull(elements);
        if (!double.IsFinite(horizontalDistance) || horizontalDistance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(horizontalDistance));
        }

        if (!double.IsFinite(verticalDistance) || verticalDistance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(verticalDistance));
        }

        var parents = Enumerable.Range(0, elements.Count).ToArray();
        for (var left = 0; left < elements.Count; left++)
        {
            var leftCenter = GetCenter(elements[left]);
            for (var right = left + 1; right < elements.Count; right++)
            {
                var rightCenter = GetCenter(elements[right]);
                if (Math.Abs(leftCenter.X - rightCenter.X) < horizontalDistance &&
                    Math.Abs(leftCenter.Y - rightCenter.Y) < verticalDistance)
                {
                    Union(parents, left, right);
                }
            }
        }

        return elements
            .Select((element, index) => (Element: element, Root: Find(parents, index)))
            .GroupBy(item => item.Root)
            .Select(group => new HintCluster(group.Select(item => item.Element).ToArray()))
            .OrderBy(cluster => cluster.CenterY)
            .ThenBy(cluster => cluster.CenterX)
            .ToArray();
    }

    private static (double X, double Y) GetCenter(DetectedElement element) =>
        (element.Bounds.X + (element.Bounds.Width / 2), element.Bounds.Y + (element.Bounds.Height / 2));

    private static int Find(int[] parents, int value)
    {
        while (parents[value] != value)
        {
            parents[value] = parents[parents[value]];
            value = parents[value];
        }

        return value;
    }

    private static void Union(int[] parents, int left, int right)
    {
        var leftRoot = Find(parents, left);
        var rightRoot = Find(parents, right);
        if (leftRoot != rightRoot)
        {
            parents[rightRoot] = leftRoot;
        }
    }
}
