using PowerMouse.ClickHints.Detection;
using PowerMouse.ClickHints.Hints;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class HintTargetPlacementEngineTests
{
    [TestMethod]
    public void CoincidentTargetsReceiveDistinctValidClickPoints()
    {
        var bounds = new ElementBounds(10, 20, 28, 28);
        var elements = new[]
        {
            Element("a", bounds, bounds),
            Element("b", bounds, bounds),
            Element("c", bounds, bounds),
        };

        var placements = HintTargetPlacementEngine.Place(elements);
        var centers = placements.Select(placement => Center(placement.ClickTarget)).ToArray();

        Assert.AreEqual(3, centers.Distinct().Count());
        Assert.IsTrue(centers.All(center => bounds.Contains(center.X, center.Y)));
    }

    [TestMethod]
    public void OriginalVisualGeometryBiasesCoincidentClickAreas()
    {
        var clickBounds = new ElementBounds(40, 40, 20, 20);
        var elements = new[]
        {
            Element("left", clickBounds, new ElementBounds(0, 30, 40, 40)),
            Element("right", clickBounds, new ElementBounds(60, 30, 40, 40)),
        };

        var placements = HintTargetPlacementEngine.Place(elements);

        Assert.IsTrue(Center(placements[0].ClickTarget).X < Center(placements[1].ClickTarget).X);
    }

    [TestMethod]
    public void SmallTargetsKeepTheResolvedAreaInsideTheirBounds()
    {
        var bounds = new ElementBounds(10, 20, 2, 2);

        var placement = HintTargetPlacementEngine.Place([Element("small", bounds, bounds)])[0];

        Assert.IsTrue(bounds.Contains(placement.ClickTarget.X, placement.ClickTarget.Y));
        Assert.IsTrue(bounds.Contains(placement.ClickTarget.Right, placement.ClickTarget.Bottom));
    }

    private static DetectedElement Element(string id, ElementBounds bounds, ElementBounds visualBounds) => new(
        id,
        bounds,
        ElementInteraction.Invoke,
        DetectionSource.UiAutomation,
        visualBounds: visualBounds);

    private static (double X, double Y) Center(ElementBounds bounds) =>
        (bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
}
