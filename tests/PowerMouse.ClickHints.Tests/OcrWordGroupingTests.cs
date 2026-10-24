using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class OcrWordGroupingTests
{
    private static readonly string[] MenuCommands = ["File", "Edit", "View", "Help"];
    [TestMethod]
    public void SpacedMenuCommandsRemainSeparateAtDifferentScales()
    {
        foreach (var scale in new[] { 1d, 1.5, 2d })
        {
            var words = MenuCommands
                .Select((text, i) => new RecognizedTextRegion(text,
                    new ElementBounds((10 + i * 50) * scale, 20 * scale, 28 * scale, 16 * scale)));
            var groups = OcrWordGrouping.GroupLine(words);
            Assert.AreEqual(4, groups.Count);
            Assert.AreEqual("Help", groups[3].Text);
        }
    }

    [TestMethod]
    public void MultiwordLabelsStayTogetherButSeparateCommandsDoNot()
    {
        var groups = OcrWordGrouping.GroupLine([
            new("Save", new(10, 20, 30, 16)),
            new("As", new(45, 20, 18, 16)),
            new("Cancel", new(100, 20, 40, 16)),
        ]);
        Assert.AreEqual(2, groups.Count);
        Assert.AreEqual("Save As", groups[0].Text);
        Assert.AreEqual(new ElementBounds(10, 20, 53, 16), groups[0].Bounds);
    }

    [TestMethod]
    public void EmptyAndInvalidWordsDoNotCreateTargets()
    {
        Assert.AreEqual(0, OcrWordGrouping.GroupLine([
            new("", new(0, 0, 10, 10)), new("bad", new(0, 0, 0, 10)),
        ]).Count);
    }
}
