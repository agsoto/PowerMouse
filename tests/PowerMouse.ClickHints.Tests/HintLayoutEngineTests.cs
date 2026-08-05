using PowerMouse.ClickHints.Detection;
using PowerMouse.ClickHints.Hints;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class HintLayoutEngineTests
{
    [TestMethod]
    public void SeparatedHintsKeepTheirPreferredPositions()
    {
        var items = new[]
        {
            new HintLayoutItem(0, 10, 10, 30, 20),
            new HintLayoutItem(1, 100, 100, 30, 20),
        };

        var result = HintLayoutEngine.Layout(items, new ElementBounds(0, 0, 300, 200));

        Assert.AreEqual(new ElementBounds(10, 10, 30, 20), result[0].Bounds);
        Assert.AreEqual(new ElementBounds(100, 100, 30, 20), result[1].Bounds);
    }

    [TestMethod]
    public void ClusteredHintsAreFullyVisibleAndDoNotOverlap()
    {
        var items = Enumerable.Range(0, 12)
            .Select(index => new HintLayoutItem(index, 90, 70, 34, 22))
            .ToArray();
        var container = new ElementBounds(0, 0, 240, 180);

        var result = HintLayoutEngine.Layout(items, container, gap: 2);

        Assert.AreEqual(items.Length, result.Count);
        Assert.IsTrue(result.All(item => IsInside(item.Bounds, container)));
        Assert.IsFalse(result.Any(left => result.Any(right =>
            left.Index != right.Index && Overlaps(left.Bounds, right.Bounds))));
    }

    [TestMethod]
    public void HintsAtDesktopEdgesAreClampedBeforeCollisionResolution()
    {
        var items = new[]
        {
            new HintLayoutItem(0, 195, 95, 30, 20),
            new HintLayoutItem(1, 195, 95, 30, 20),
        };
        var container = new ElementBounds(0, 0, 200, 100);

        var result = HintLayoutEngine.Layout(items, container, gap: 2);

        Assert.IsTrue(result.All(item => IsInside(item.Bounds, container)));
        Assert.IsFalse(Overlaps(result[0].Bounds, result[1].Bounds));
    }

    [TestMethod]
    public void LargeSinglePointClusterUsesAvailableDesktopSpace()
    {
        var items = Enumerable.Range(0, 100)
            .Select(index => new HintLayoutItem(index, 390, 290, 34, 22))
            .ToArray();
        var container = new ElementBounds(0, 0, 800, 600);

        var result = HintLayoutEngine.Layout(items, container, gap: 2);

        Assert.AreEqual(items.Length, result.Count);
        Assert.IsTrue(result.All(item => IsInside(item.Bounds, container)));
        Assert.IsFalse(result.Any(left => result.Any(right =>
            left.Index != right.Index && Overlaps(left.Bounds, right.Bounds))));
    }

    private static bool IsInside(ElementBounds item, ElementBounds container) =>
        item.X >= container.X &&
        item.Y >= container.Y &&
        item.Right <= container.Right &&
        item.Bottom <= container.Bottom;

    private static bool Overlaps(ElementBounds left, ElementBounds right) =>
        left.X < right.Right && left.Right > right.X && left.Y < right.Bottom && left.Bottom > right.Y;
}
