namespace PowerMouse.ClickHints.Detection;

/// <summary>Separates spaced commands without splitting ordinary multiword labels.</summary>
public static class OcrWordGrouping
{
    public static IReadOnlyList<RecognizedTextRegion> GroupLine(IEnumerable<RecognizedTextRegion> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var groups = new List<RecognizedTextRegion>();
        RecognizedTextRegion? current = null;
        foreach (var word in words.Where(word => !word.Bounds.IsEmpty && !string.IsNullOrWhiteSpace(word.Text))
                     .OrderBy(word => word.Bounds.X))
        {
            if (current is null)
            {
                current = word;
                continue;
            }

            // Relative to text height so the same decision survives DPI scaling.
            var gap = word.Bounds.X - current.Bounds.Right;
            var height = Math.Min(current.Bounds.Height, word.Bounds.Height);
            if (gap > height * 0.75)
            {
                groups.Add(current);
                current = word;
                continue;
            }

            var x = Math.Min(current.Bounds.X, word.Bounds.X);
            var y = Math.Min(current.Bounds.Y, word.Bounds.Y);
            current = new RecognizedTextRegion(
                current.Text + " " + word.Text,
                new ElementBounds(x, y, Math.Max(current.Bounds.Right, word.Bounds.Right) - x,
                    Math.Max(current.Bounds.Bottom, word.Bounds.Bottom) - y),
                Math.Min(current.Confidence, word.Confidence));
        }

        if (current is not null) groups.Add(current);
        return groups;
    }
}
