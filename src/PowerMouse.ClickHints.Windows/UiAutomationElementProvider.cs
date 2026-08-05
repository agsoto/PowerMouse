using System.Globalization;
using System.Windows.Automation;
using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Windows;

public sealed class UiAutomationElementProvider : IElementDetectionProvider
{
    public string Name => "Windows UI Automation";

    public ValueTask<DetectionProviderResult> DetectAsync(
        DetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // UIA providers are cross-process COM servers. Keep their synchronous work
        // away from the caller's UI thread and enforce the outer pipeline budget.
        return new ValueTask<DetectionProviderResult>(Task.Run(
            () => Detect(request, cancellationToken),
            cancellationToken));
    }

    private static DetectionProviderResult Detect(
        DetectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var root = AutomationElement.FromHandle(request.TargetWindow);
            if (root is null)
            {
                return DetectionProviderResult.Failure("The target window has no UI Automation root.");
            }

            var cache = CreateCacheRequest();
            AutomationElementCollection descendants;
            using (cache.Activate())
            {
                descendants = root.FindAll(TreeScope.Descendants, Automation.ControlViewCondition);
            }

            var rawTargetLimit = checked((int)Math.Min(
                descendants.Count,
                (long)request.Options.MaximumTargets * 4));
            var elements = new List<DetectedElement>(rawTargetLimit);
            foreach (AutomationElement descendant in descendants)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var classified = UiaElementClassifier.Classify(ToSnapshot(descendant));
                if (classified is not null)
                {
                    elements.Add(classified);
                    if (elements.Count == rawTargetLimit)
                    {
                        break;
                    }
                }
            }

            return DetectionProviderResult.Success(elements);
        }
        catch (ElementNotAvailableException)
        {
            return DetectionProviderResult.Failure("The target UI changed while it was being inspected.");
        }
        catch (InvalidOperationException exception)
        {
            return DetectionProviderResult.Failure(exception.Message);
        }
    }

    private static CacheRequest CreateCacheRequest()
    {
        var cache = new CacheRequest
        {
            AutomationElementMode = AutomationElementMode.None,
            TreeFilter = Automation.ControlViewCondition,
            TreeScope = TreeScope.Element,
        };
        cache.Add(AutomationElement.AutomationIdProperty);
        cache.Add(AutomationElement.BoundingRectangleProperty);
        cache.Add(AutomationElement.ControlTypeProperty);
        cache.Add(AutomationElement.IsEnabledProperty);
        cache.Add(AutomationElement.IsExpandCollapsePatternAvailableProperty);
        cache.Add(AutomationElement.IsInvokePatternAvailableProperty);
        cache.Add(AutomationElement.IsKeyboardFocusableProperty);
        cache.Add(AutomationElement.IsOffscreenProperty);
        cache.Add(AutomationElement.IsRangeValuePatternAvailableProperty);
        cache.Add(AutomationElement.IsSelectionItemPatternAvailableProperty);
        cache.Add(AutomationElement.IsTogglePatternAvailableProperty);
        cache.Add(AutomationElement.IsValuePatternAvailableProperty);
        cache.Add(AutomationElement.NameProperty);
        cache.Add(AutomationElement.RuntimeIdProperty);
        return cache;
    }

    private static UiaElementSnapshot ToSnapshot(AutomationElement element)
    {
        var cached = element.Cached;
        var rectangle = cached.BoundingRectangle;
        return new UiaElementSnapshot(
            GetElementId(element, cached.AutomationId, rectangle),
            cached.Name,
            new ElementBounds(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height),
            MapControlType(cached.ControlType),
            cached.IsEnabled,
            cached.IsOffscreen,
            cached.IsKeyboardFocusable,
            GetCachedBoolean(element, AutomationElement.IsInvokePatternAvailableProperty),
            GetCachedBoolean(element, AutomationElement.IsTogglePatternAvailableProperty),
            GetCachedBoolean(element, AutomationElement.IsSelectionItemPatternAvailableProperty),
            GetCachedBoolean(element, AutomationElement.IsExpandCollapsePatternAvailableProperty),
            GetCachedBoolean(element, AutomationElement.IsValuePatternAvailableProperty),
            GetCachedBoolean(element, AutomationElement.IsRangeValuePatternAvailableProperty));
    }

    private static string GetElementId(AutomationElement element, string automationId, System.Windows.Rect bounds)
    {
        var runtimeId = element.GetCachedPropertyValue(AutomationElement.RuntimeIdProperty) as int[];
        if (runtimeId is { Length: > 0 })
        {
            return "uia:" + string.Join(
                '.',
                runtimeId.Select(value => value.ToString(CultureInfo.InvariantCulture)));
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"uia:{automationId}:{bounds.X:0}:{bounds.Y:0}:{bounds.Width:0}:{bounds.Height:0}");
    }

    private static bool GetCachedBoolean(AutomationElement element, AutomationProperty property) =>
        element.GetCachedPropertyValue(property) is true;

    private static UiaControlType MapControlType(ControlType controlType)
    {
        if (controlType == ControlType.Button) return UiaControlType.Button;
        if (controlType == ControlType.Calendar) return UiaControlType.Calendar;
        if (controlType == ControlType.CheckBox) return UiaControlType.CheckBox;
        if (controlType == ControlType.ComboBox) return UiaControlType.ComboBox;
        if (controlType == ControlType.DataItem) return UiaControlType.DataItem;
        if (controlType == ControlType.Document) return UiaControlType.Document;
        if (controlType == ControlType.Edit) return UiaControlType.Edit;
        if (controlType == ControlType.Group) return UiaControlType.Group;
        if (controlType == ControlType.Hyperlink) return UiaControlType.Hyperlink;
        if (controlType == ControlType.List) return UiaControlType.List;
        if (controlType == ControlType.ListItem) return UiaControlType.ListItem;
        if (controlType == ControlType.Menu) return UiaControlType.Menu;
        if (controlType == ControlType.MenuItem) return UiaControlType.MenuItem;
        if (controlType == ControlType.Pane) return UiaControlType.Pane;
        if (controlType == ControlType.RadioButton) return UiaControlType.RadioButton;
        if (controlType == ControlType.Slider) return UiaControlType.Slider;
        if (controlType == ControlType.Spinner) return UiaControlType.Spinner;
        if (controlType == ControlType.SplitButton) return UiaControlType.SplitButton;
        if (controlType == ControlType.TabItem) return UiaControlType.TabItem;
        if (controlType == ControlType.Text) return UiaControlType.Text;
        if (controlType == ControlType.ToolBar) return UiaControlType.ToolBar;
        if (controlType == ControlType.TreeItem) return UiaControlType.TreeItem;
        return UiaControlType.Unknown;
    }
}
