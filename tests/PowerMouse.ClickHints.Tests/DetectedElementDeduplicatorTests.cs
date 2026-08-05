using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class DetectedElementDeduplicatorTests
{
    [TestMethod]
    public void CoincidentUiaAliasesKeepTheMostSemanticControl()
    {
        var bounds = new ElementBounds(10, 20, 100, 40);
        var elements = new[]
        {
            Element("group", bounds, UiaControlType.Unknown),
            Element("item", bounds, UiaControlType.DataItem),
            Element("button", bounds, UiaControlType.Button),
        };

        var result = DetectedElementDeduplicator.Deduplicate(elements);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("button", result[0].Id);
    }

    [TestMethod]
    public void CentersWithinEightPixelsRepresentTheSamePhysicalClick()
    {
        var elements = new[]
        {
            Element("first", new ElementBounds(10, 10, 20, 20), UiaControlType.Button),
            Element("alias", new ElementBounds(15, 12, 20, 20), UiaControlType.Unknown),
        };

        var result = DetectedElementDeduplicator.Deduplicate(elements);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("first", result[0].Id);
    }

    [TestMethod]
    public void NearbyButDistinctTargetsRemainSeparate()
    {
        var elements = new[]
        {
            Element("first", new ElementBounds(10, 10, 20, 20), UiaControlType.Button),
            Element("second", new ElementBounds(30, 10, 20, 20), UiaControlType.Button),
        };

        var result = DetectedElementDeduplicator.Deduplicate(elements);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void StronglyOverlappingAliasesAreDeduplicatedDespiteCenterRounding()
    {
        var elements = new[]
        {
            Element("first", new ElementBounds(10, 10, 100, 40), UiaControlType.Hyperlink),
            Element("alias", new ElementBounds(12, 11, 100, 40), UiaControlType.Unknown),
        };

        var result = DetectedElementDeduplicator.Deduplicate(elements);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("first", result[0].Id);
    }

    private static DetectedElement Element(string id, ElementBounds bounds, UiaControlType controlType) => new(
        id,
        bounds,
        ElementInteraction.Invoke,
        DetectionSource.UiAutomation,
        controlType: controlType);
}
