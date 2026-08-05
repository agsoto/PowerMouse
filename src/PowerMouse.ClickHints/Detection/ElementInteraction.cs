namespace PowerMouse.ClickHints.Detection;

[Flags]
public enum ElementInteraction
{
    None = 0,
    Invoke = 1 << 0,
    Toggle = 1 << 1,
    Select = 1 << 2,
    ExpandCollapse = 1 << 3,
    Edit = 1 << 4,
    Focus = 1 << 5,
    ApproximateClick = 1 << 6,
}
