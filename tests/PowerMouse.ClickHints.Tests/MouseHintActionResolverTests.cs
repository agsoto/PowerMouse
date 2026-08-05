using PowerMouse.ClickHints.Hints;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class MouseHintActionResolverTests
{
    [TestMethod]
    [DataRow(true, false, MouseHintAction.Click)]
    [DataRow(true, true, MouseHintAction.Move)]
    [DataRow(false, false, MouseHintAction.Move)]
    [DataRow(false, true, MouseHintAction.Click)]
    public void ModifierInvertsConfiguredAction(
        bool automaticClick,
        bool invert,
        MouseHintAction expected)
    {
        Assert.AreEqual(expected, MouseHintActionResolver.Resolve(automaticClick, invert));
    }
}
