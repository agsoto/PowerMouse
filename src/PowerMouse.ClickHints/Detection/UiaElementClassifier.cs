namespace PowerMouse.ClickHints.Detection;

public static class UiaElementClassifier
{
    public static DetectedElement? Classify(UiaElementSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.IsEnabled || snapshot.Bounds.IsEmpty)
        {
            return null;
        }

        // Browser accessibility providers may expose Invoke on structural nodes.
        // Their actionable descendants are more precise and avoid blank-page hints.
        if (IsStructural(snapshot.ControlType))
        {
            return null;
        }

        // Firefox sometimes marks visible chrome buttons as offscreen. Only strong
        // interactive roles can override that flag; request clipping handles scrolling.
        if (snapshot.IsOffscreen && !CanOverrideOffscreen(snapshot.ControlType))
        {
            return null;
        }

        var interaction = ElementInteraction.None;
        interaction |= snapshot.SupportsInvoke ? ElementInteraction.Invoke : ElementInteraction.None;
        interaction |= snapshot.SupportsToggle ? ElementInteraction.Toggle : ElementInteraction.None;
        interaction |= snapshot.SupportsSelectionItem ? ElementInteraction.Select : ElementInteraction.None;
        interaction |= snapshot.SupportsExpandCollapse ? ElementInteraction.ExpandCollapse : ElementInteraction.None;
        interaction |= snapshot.SupportsValue || snapshot.SupportsRangeValue ? ElementInteraction.Edit : ElementInteraction.None;
        interaction |= snapshot.IsKeyboardFocusable ? ElementInteraction.Focus : ElementInteraction.None;

        if (interaction == ElementInteraction.None && !IsConventionallyInteractive(snapshot.ControlType))
        {
            return null;
        }

        if (interaction == ElementInteraction.None)
        {
            interaction = ElementInteraction.ApproximateClick;
        }

        return new DetectedElement(
            snapshot.Id,
            GetTargetBounds(snapshot),
            interaction,
            DetectionSource.UiAutomation,
            snapshot.Name,
            visualBounds: snapshot.Bounds,
            controlType: snapshot.ControlType);
    }

    private static ElementBounds GetTargetBounds(UiaElementSnapshot snapshot)
    {
        if (!snapshot.SupportsExpandCollapse || snapshot.ControlType == UiaControlType.Button)
        {
            return snapshot.Bounds;
        }

        const double maximumAffordanceSize = 40;
        var size = Math.Min(
            maximumAffordanceSize,
            Math.Min(snapshot.Bounds.Width, snapshot.Bounds.Height));
        var useLeftEdge = snapshot.ControlType is
            UiaControlType.TreeItem or
            UiaControlType.ListItem or
            UiaControlType.DataItem;
        var x = useLeftEdge
            ? snapshot.Bounds.X
            : snapshot.Bounds.Right - size;
        return new ElementBounds(x, snapshot.Bounds.Y, size, size);
    }

    private static bool IsConventionallyInteractive(UiaControlType controlType) => controlType is
        UiaControlType.Button or
        UiaControlType.Calendar or
        UiaControlType.CheckBox or
        UiaControlType.ComboBox or
        UiaControlType.DataItem or
        UiaControlType.Edit or
        UiaControlType.Hyperlink or
        UiaControlType.ListItem or
        UiaControlType.MenuItem or
        UiaControlType.RadioButton or
        UiaControlType.Slider or
        UiaControlType.Spinner or
        UiaControlType.SplitButton or
        UiaControlType.TabItem or
        UiaControlType.TreeItem;

    private static bool CanOverrideOffscreen(UiaControlType controlType) => controlType is
        UiaControlType.Button or
        UiaControlType.CheckBox or
        UiaControlType.ComboBox or
        UiaControlType.Edit or
        UiaControlType.MenuItem or
        UiaControlType.RadioButton or
        UiaControlType.Slider or
        UiaControlType.Spinner or
        UiaControlType.SplitButton or
        UiaControlType.TabItem;

    private static bool IsStructural(UiaControlType controlType) => controlType is
        UiaControlType.Document or
        UiaControlType.Group or
        UiaControlType.List or
        UiaControlType.Menu or
        UiaControlType.Pane or
        UiaControlType.Text or
        UiaControlType.ToolBar;
}
