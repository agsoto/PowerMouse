using PowerMouse.ClickHints.Detection;
using PowerMouse.ClickHints.Hints;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class HintClusterEngineTests
{
    [TestMethod]
    public void NearbyTargetsFormOneTransitiveCluster()
    {
        var elements = new[]
        {
            Element("a", 10, 10),
            Element("b", 70, 10),
            Element("c", 130, 10),
        };

        var clusters = HintClusterEngine.Cluster(elements, horizontalDistance: 72, verticalDistance: 32);

        Assert.AreEqual(1, clusters.Count);
        Assert.AreEqual(3, clusters[0].Elements.Count);
    }

    [TestMethod]
    public void DistantTargetsRemainAtTheirOwnClickPoints()
    {
        var elements = new[]
        {
            Element("a", 10, 10),
            Element("b", 200, 100),
        };

        var clusters = HintClusterEngine.Cluster(elements);

        Assert.AreEqual(2, clusters.Count);
        Assert.IsTrue(clusters.All(cluster => !cluster.IsCluster));
        Assert.AreEqual(15, clusters[0].CenterX);
        Assert.AreEqual(15, clusters[0].CenterY);
    }

    private static DetectedElement Element(string id, double x, double y) => new(
        id,
        new ElementBounds(x, y, 10, 10),
        ElementInteraction.Invoke,
        DetectionSource.UiAutomation);
}
