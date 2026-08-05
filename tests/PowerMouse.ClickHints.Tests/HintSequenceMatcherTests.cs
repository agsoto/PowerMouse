using PowerMouse.ClickHints.Detection;
using PowerMouse.ClickHints.Hints;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class HintSequenceMatcherTests
{
    private static readonly string[] ExpectedPrefixMatches = ["AA", "AS"];

    [TestMethod]
    public void PrefixFiltersCandidatesAndExactCodeSelects()
    {
        var matcher = new HintSequenceMatcher();
        matcher.Reset([Hint("AA"), Hint("AS"), Hint("SA")]);

        var first = matcher.Append('a');
        var second = matcher.Append('s');

        CollectionAssert.AreEqual(ExpectedPrefixMatches, first.Candidates.Select(x => x.Keys).ToArray());
        Assert.IsNull(first.Selected);
        Assert.AreEqual("AS", second.Selected?.Keys);
    }

    [TestMethod]
    public void BackspaceRestoresCandidatesAndUnknownPrefixIsDeadEnd()
    {
        var matcher = new HintSequenceMatcher();
        matcher.Reset([Hint("AA"), Hint("AS")]);
        matcher.Append('a');
        matcher.Append('a');

        var restored = matcher.Backspace();
        var deadEnd = matcher.Append('z');

        Assert.AreEqual(2, restored.Candidates.Count);
        Assert.IsTrue(deadEnd.IsDeadEnd);
    }

    private static ClickHint Hint(string keys) => new(
        keys,
        new DetectedElement(
            keys,
            new ElementBounds(0, 0, 10, 10),
            ElementInteraction.Invoke,
            DetectionSource.UiAutomation));
}
