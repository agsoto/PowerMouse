namespace PowerMouse.ClickHints.Hints;

public static class MouseHintActionResolver
{
    public static MouseHintAction Resolve(bool automaticClick, bool invert) =>
        automaticClick ^ invert ? MouseHintAction.Click : MouseHintAction.Move;
}
