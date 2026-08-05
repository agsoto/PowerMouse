namespace PowerMouse.ClickHints.Hints;

public sealed record HintMatchResult(
    IReadOnlyList<ClickHint> Candidates,
    ClickHint? Selected,
    bool IsDeadEnd);
