namespace PowerMouse.ClickHints.Detection;

public static class DetectedElementDeduplicator
{
    private const double EquivalentClickDistance = 8;
    private const double EquivalentBoundsOverlap = 0.85;

    public static IReadOnlyList<DetectedElement> Deduplicate(IReadOnlyList<DetectedElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        var selected = new List<(DetectedElement Element, int OriginalIndex)>(elements.Count);
        foreach (var candidate in elements
                     .Select((element, index) => (Element: element, OriginalIndex: index))
                     .OrderByDescending(item => SemanticRank(item.Element.ControlType))
                     .ThenByDescending(item => InteractionRank(item.Element.Interaction))
                     .ThenByDescending(item => item.Element.Name is not null)
                     .ThenBy(item => Area(item.Element.Bounds))
                     .ThenByDescending(item => item.Element.Confidence)
                     .ThenBy(item => item.OriginalIndex))
        {
            if (selected.All(existing => !IsEquivalentClick(existing.Element, candidate.Element)))
            {
                selected.Add(candidate);
            }
        }

        return selected
            .OrderBy(item => item.OriginalIndex)
            .Select(item => item.Element)
            .ToArray();
    }

    private static bool IsEquivalentClick(DetectedElement left, DetectedElement right)
    {
        var deltaX = CenterX(left.Bounds) - CenterX(right.Bounds);
        var deltaY = CenterY(left.Bounds) - CenterY(right.Bounds);
        return (deltaX * deltaX) + (deltaY * deltaY) <= EquivalentClickDistance * EquivalentClickDistance ||
               left.Bounds.IntersectionOverUnion(right.Bounds) >= EquivalentBoundsOverlap;
    }

    private static int SemanticRank(UiaControlType type) => type switch
    {
        UiaControlType.Button or
        UiaControlType.Calendar or
        UiaControlType.CheckBox or
        UiaControlType.ComboBox or
        UiaControlType.Edit or
        UiaControlType.Hyperlink or
        UiaControlType.MenuItem or
        UiaControlType.RadioButton or
        UiaControlType.Slider or
        UiaControlType.Spinner or
        UiaControlType.SplitButton or
        UiaControlType.TabItem => 3,
        UiaControlType.DataItem or
        UiaControlType.ListItem or
        UiaControlType.TreeItem => 2,
        _ => 1,
    };

    private static int InteractionRank(ElementInteraction interaction)
    {
        var actionable = interaction & ~ElementInteraction.Focus & ~ElementInteraction.ApproximateClick;
        if (actionable != ElementInteraction.None)
        {
            return 3;
        }

        return interaction.HasFlag(ElementInteraction.ApproximateClick) ? 2 : 1;
    }

    private static double Area(ElementBounds bounds) => bounds.Width * bounds.Height;

    private static double CenterX(ElementBounds bounds) => bounds.X + (bounds.Width / 2);

    private static double CenterY(ElementBounds bounds) => bounds.Y + (bounds.Height / 2);
}
