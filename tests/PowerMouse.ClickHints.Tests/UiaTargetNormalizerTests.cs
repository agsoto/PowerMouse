using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class UiaTargetNormalizerTests
{
    [TestMethod]
    public void ListRowUsesItsNameCellAndDropsOnlyReadOnlyMetadata()
    {
        var row = Snapshot("row", UiaControlType.ListItem, new(0, 0, 500, 40)) with { SupportsSelectionItem = true };
        var name = Snapshot("name", UiaControlType.Edit, new(20, 5, 180, 30)) with { SupportsValue = true };
        var date = Snapshot("date", UiaControlType.Edit, new(200, 5, 100, 30)) with { SupportsValue = true, IsValueReadOnly = true };
        var editable = Snapshot("editable", UiaControlType.Edit, new(300, 5, 100, 30)) with { SupportsValue = true };
        var button = Snapshot("button", UiaControlType.Button, new(450, 5, 30, 30)) with { SupportsInvoke = true };
        var result = UiaTargetNormalizer.Classify([row, name, date, editable, button]);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(name.Bounds, result.Single(e => e.Id == "row").Bounds);
        Assert.AreEqual(row.Bounds, result.Single(e => e.Id == "row").VisualBounds);
        Assert.IsTrue(result.Any(e => e.Id == "editable"));
        Assert.IsTrue(result.Any(e => e.Id == "button"));
    }

    [TestMethod]
    public void StandaloneReadOnlyTextFieldRemainsReachable()
    {
        var field = Snapshot("field", UiaControlType.Edit, new(0, 0, 100, 30)) with
            { SupportsValue = true, IsValueReadOnly = true };
        Assert.AreEqual(1, UiaTargetNormalizer.Classify([field]).Count);
    }

    [TestMethod]
    public void DisabledRowsCannotSuppressTheirChildren()
    {
        var row = Snapshot("row", UiaControlType.ListItem, new(0, 0, 200, 40)) with
            { SupportsSelectionItem = true, IsEnabled = false };
        var field = Snapshot("field", UiaControlType.Edit, new(20, 5, 100, 30)) with { SupportsValue = true };
        Assert.AreEqual("field", UiaTargetNormalizer.Classify([row, field]).Single().Id);
    }

    private static UiaElementSnapshot Snapshot(string id, UiaControlType type, ElementBounds bounds) =>
        new(id, id, bounds, type, true, false, false, false, false, false, false, false, false);
}
