using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class UiaElementClassifierTests
{
    [TestMethod]
    public void InteractivePatternsAreCombined()
    {
        var snapshot = Snapshot() with
        {
            SupportsInvoke = true,
            SupportsToggle = true,
            IsKeyboardFocusable = true,
        };

        var element = UiaElementClassifier.Classify(snapshot);

        Assert.IsNotNull(element);
        Assert.IsTrue(element.Interaction.HasFlag(ElementInteraction.Invoke));
        Assert.IsTrue(element.Interaction.HasFlag(ElementInteraction.Toggle));
        Assert.IsTrue(element.Interaction.HasFlag(ElementInteraction.Focus));
    }

    [TestMethod]
    public void DisabledAndNonInteractiveElementsAreRejected()
    {
        Assert.IsNull(UiaElementClassifier.Classify(Snapshot() with { IsEnabled = false, SupportsInvoke = true }));
        Assert.IsNull(UiaElementClassifier.Classify(Snapshot()));
    }

    [TestMethod]
    public void OffscreenFlagDoesNotOverrideValidGeometry()
    {
        var element = UiaElementClassifier.Classify(Snapshot() with
        {
            IsOffscreen = true,
            ControlType = UiaControlType.Button,
            SupportsInvoke = true,
        });

        Assert.IsNotNull(element);
    }

    [TestMethod]
    public void OffscreenGenericTargetIsRejected()
    {
        var element = UiaElementClassifier.Classify(Snapshot() with
        {
            IsOffscreen = true,
            SupportsInvoke = true,
        });

        Assert.IsNull(element);
    }

    [TestMethod]
    [DataRow(UiaControlType.Document)]
    [DataRow(UiaControlType.Group)]
    [DataRow(UiaControlType.Pane)]
    [DataRow(UiaControlType.Text)]
    public void StructuralInvokeAliasesAreRejected(UiaControlType controlType)
    {
        var element = UiaElementClassifier.Classify(Snapshot() with
        {
            ControlType = controlType,
            SupportsInvoke = true,
        });

        Assert.IsNull(element);
    }

    [TestMethod]
    public void ConventionalControlWithoutPatternsRemainsAnApproximateTarget()
    {
        var element = UiaElementClassifier.Classify(Snapshot() with { ControlType = UiaControlType.Button });

        Assert.IsNotNull(element);
        Assert.AreEqual(ElementInteraction.ApproximateClick, element.Interaction);
    }

    [TestMethod]
    public void ExpandableTreeItemTargetsItsLeftDisclosureControl()
    {
        var element = UiaElementClassifier.Classify(Snapshot() with
        {
            Bounds = new ElementBounds(10, 20, 300, 28),
            ControlType = UiaControlType.TreeItem,
            SupportsExpandCollapse = true,
        });

        Assert.IsNotNull(element);
        Assert.AreEqual(new ElementBounds(10, 20, 28, 28), element.Bounds);
        Assert.AreEqual(new ElementBounds(10, 20, 300, 28), element.VisualBounds);
    }

    [TestMethod]
    public void ExpandableComboBoxTargetsItsRightDisclosureControl()
    {
        var element = UiaElementClassifier.Classify(Snapshot() with
        {
            Bounds = new ElementBounds(10, 20, 300, 32),
            ControlType = UiaControlType.ComboBox,
            SupportsExpandCollapse = true,
        });

        Assert.IsNotNull(element);
        Assert.AreEqual(new ElementBounds(278, 20, 32, 32), element.Bounds);
    }

    [TestMethod]
    public void ExpandableButtonKeepsItsFullClickableBounds()
    {
        var bounds = new ElementBounds(10, 20, 300, 32);
        var element = UiaElementClassifier.Classify(Snapshot() with
        {
            Bounds = bounds,
            ControlType = UiaControlType.Button,
            SupportsExpandCollapse = true,
        });

        Assert.IsNotNull(element);
        Assert.AreEqual(bounds, element.Bounds);
    }

    private static UiaElementSnapshot Snapshot() => new(
        "uia:1",
        "Target",
        new ElementBounds(10, 10, 50, 20),
        UiaControlType.Unknown,
        IsEnabled: true,
        IsOffscreen: false,
        IsKeyboardFocusable: false,
        SupportsInvoke: false,
        SupportsToggle: false,
        SupportsSelectionItem: false,
        SupportsExpandCollapse: false,
        SupportsValue: false,
        SupportsRangeValue: false);
}
