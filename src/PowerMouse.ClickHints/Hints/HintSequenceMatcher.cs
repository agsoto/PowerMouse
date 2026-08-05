namespace PowerMouse.ClickHints.Hints;

public sealed class HintSequenceMatcher
{
    private IReadOnlyList<ClickHint> hints = [];

    public string Prefix { get; private set; } = string.Empty;

    public void Reset(IReadOnlyList<ClickHint> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        hints = value;
        Prefix = string.Empty;
    }

    public HintMatchResult Append(char character)
    {
        Prefix += char.ToUpperInvariant(character);
        return Match();
    }

    public HintMatchResult Backspace()
    {
        if (Prefix.Length > 0)
        {
            Prefix = Prefix[..^1];
        }

        return Match();
    }

    private HintMatchResult Match()
    {
        var candidates = hints
            .Where(hint => hint.Keys.StartsWith(Prefix, StringComparison.Ordinal))
            .ToArray();
        var selected = candidates.SingleOrDefault(hint =>
            string.Equals(hint.Keys, Prefix, StringComparison.Ordinal));
        return new HintMatchResult(candidates, selected, candidates.Length == 0);
    }
}
