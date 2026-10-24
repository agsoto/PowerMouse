namespace PowerMouse.ClickHints.Detection;

public static class UiaTargetNormalizer
{
    public static IReadOnlyList<DetectedElement> Classify(IReadOnlyList<UiaElementSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var rows = snapshots.Where(s => s.ControlType == UiaControlType.ListItem &&
            s.SupportsSelectionItem && s.IsEnabled && !s.IsOffscreen && !s.Bounds.IsEmpty).ToArray();
        var rowLabels = new Dictionary<string, UiaElementSnapshot>();
        var redundantCells = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var cells = snapshots.Where(s => s.ControlType == UiaControlType.Edit &&
                s.IsEnabled && !s.IsOffscreen && !s.Bounds.IsEmpty &&
                s.Bounds.Y >= row.Bounds.Y && s.Bounds.Bottom <= row.Bounds.Bottom &&
                s.Bounds.X >= row.Bounds.X &&
                row.Bounds.Contains(s.Bounds.X + s.Bounds.Width / 2, s.Bounds.Y + s.Bounds.Height / 2))
                .OrderBy(s => s.Bounds.X).ToArray();
            // Explorer exposes the name and read-only metadata as Edit children.
            // Keep one row hint on the name; retain other writable cells/actions.
            var label = cells.FirstOrDefault(s => s.SupportsValue && !s.IsValueReadOnly);
            if (label is not null)
            {
                rowLabels[row.Id] = label;
                redundantCells.Add(label.Id);
            }
            foreach (var cell in cells.Where(s => s.SupportsValue && s.IsValueReadOnly &&
                         !s.SupportsInvoke && !s.SupportsToggle && !s.SupportsExpandCollapse))
            {
                redundantCells.Add(cell.Id);
            }
        }

        var result = new List<DetectedElement>();
        foreach (var snapshot in snapshots)
        {
            if (redundantCells.Contains(snapshot.Id)) continue;
            var element = UiaElementClassifier.Classify(snapshot);
            if (element is null) continue;
            if (rowLabels.TryGetValue(snapshot.Id, out var label))
            {
                element = new DetectedElement(element.Id, label.Bounds, element.Interaction, element.Source,
                    element.Name, element.Confidence, element.VisualBounds, element.ControlType);
            }
            result.Add(element);
        }
        return result;
    }
}
