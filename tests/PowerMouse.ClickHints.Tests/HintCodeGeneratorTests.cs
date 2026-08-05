using System.Globalization;
using PowerMouse.ClickHints.Detection;
using PowerMouse.ClickHints.Hints;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class HintCodeGeneratorTests
{
    private static readonly string[] ExpectedCodes = ["AAA", "AAS", "ASA", "ASS", "SAA"];

    [TestMethod]
    public void CodesAreDeterministicAndPrefixFree()
    {
        var elements = Enumerable.Range(0, 5)
            .Select(index => new DetectedElement(
                index.ToString(CultureInfo.InvariantCulture),
                new ElementBounds(index * 10, 0, 5, 5),
                ElementInteraction.Invoke,
                DetectionSource.UiAutomation))
            .ToArray();
        var generator = new HintCodeGenerator("AS");

        var hints = generator.Assign(elements);

        CollectionAssert.AreEqual(ExpectedCodes, hints.Select(x => x.Keys).ToArray());
        Assert.IsFalse(hints.Any(left => hints.Any(right => left != right && right.Keys.StartsWith(left.Keys, StringComparison.Ordinal))));
    }
}
