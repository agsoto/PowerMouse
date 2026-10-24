using System.Globalization;
using System.Windows.Automation;
using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Windows;

public sealed class UiAutomationElementProvider : IIncrementalElementDetectionProvider
{
    public string Name => "Windows UI Automation";

    public ValueTask<DetectionProviderResult> DetectAsync(
        DetectionRequest request,
        CancellationToken cancellationToken = default) =>
        DetectAsync(request, _ => { }, cancellationToken);

    public ValueTask<DetectionProviderResult> DetectAsync(
        DetectionRequest request,
        Action<IReadOnlyList<DetectedElement>> reportPartial,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(reportPartial);

        // UIA providers are cross-process COM servers. Keep their synchronous work
        // away from the caller's UI thread and enforce the outer pipeline budget.
        return new ValueTask<DetectionProviderResult>(Task.Run(
            () => Detect(request, reportPartial, cancellationToken),
            cancellationToken));
    }

    private static DetectionProviderResult Detect(
        DetectionRequest request,
        Action<IReadOnlyList<DetectedElement>> reportPartial,
        CancellationToken cancellationToken)
    {
        try
        {
            var root = AutomationElement.FromHandle(request.TargetWindow);
            if (root is null)
            {
                return DetectionProviderResult.Failure("The target window has no UI Automation root.");
            }

            var priorityRegions = FindPriorityRegions(root, request.TargetBounds);
            var cache = CreateCacheRequest();
            var snapshots = new List<UiaElementSnapshot>();
            using (cache.Activate())
            {
                // Filter before fetching the expensive action properties. Explorer's
                // navigation tree includes many unrealized/offscreen descendants.
                // Preserve the classifier's workaround for misreported chrome roles.
                var chromeRoles = new OrCondition(new[]
                {
                    ControlType.Button, ControlType.CheckBox, ControlType.ComboBox, ControlType.Edit,
                    ControlType.MenuItem, ControlType.RadioButton, ControlType.Slider,
                    ControlType.Spinner, ControlType.SplitButton, ControlType.TabItem,
                }.Select(type => (Condition)new PropertyCondition(AutomationElement.ControlTypeProperty, type)).ToArray());
                var candidates = new AndCondition(Automation.ControlViewCondition,
                    new PropertyCondition(AutomationElement.IsEnabledProperty, true),
                    new OrCondition(new PropertyCondition(AutomationElement.IsOffscreenProperty, false), chromeRoles));
                // Search compact top-level chrome first: filtering roles across
                // the entire tree still visits Explorer's large navigation tree.
                // Publish each region before attempting the rest of the window.
                var priorityRoles = new OrCondition(new[]
                {
                    ControlType.Button, ControlType.CheckBox, ControlType.ComboBox,
                    ControlType.MenuItem, ControlType.RadioButton, ControlType.SplitButton,
                    ControlType.TabItem, ControlType.Hyperlink,
                }.Select(type => (Condition)new PropertyCondition(AutomationElement.ControlTypeProperty, type)).ToArray());
                foreach (var region in priorityRegions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AddSnapshots(region.FindAll(TreeScope.Subtree, new AndCondition(candidates, priorityRoles)));
                    reportPartial(UiaTargetNormalizer.Classify(snapshots));
                }
                cancellationToken.ThrowIfCancellationRequested();
                // The complete pass refreshes the small priority set as well.
                // Deduplicate identities before row normalization.
                AddSnapshots(root.FindAll(TreeScope.Descendants, candidates));
            }

            var rawTargetLimit = checked((int)Math.Min(
                snapshots.Count,
                (long)request.Options.MaximumTargets * 4));
            return DetectionProviderResult.Success(UiaTargetNormalizer.Classify(
                snapshots.GroupBy(s => s.Id).Select(group => group.Last()).ToArray()).Take(rawTargetLimit));

            void AddSnapshots(AutomationElementCollection elements)
            {
                foreach (AutomationElement element in elements)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    snapshots.Add(ToSnapshot(element));
                }
            }
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

    private static AutomationElement[] FindPriorityRegions(AutomationElement root, ElementBounds window)
    {
        var cache = new CacheRequest { AutomationElementMode = AutomationElementMode.Full, TreeScope = TreeScope.Element };
        cache.Add(AutomationElement.BoundingRectangleProperty);
        using (cache.Activate())
        {
            var regions = root.FindAll(TreeScope.Children, Automation.ControlViewCondition)
                .Cast<AutomationElement>()
                .Where(element =>
                {
                    var bounds = element.Cached.BoundingRectangle;
                    return !bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0 &&
                           bounds.Height <= window.Height * 0.35 &&
                           bounds.Width * bounds.Height <= window.Width * window.Height * 0.4 &&
                           bounds.Top < window.Y + window.Height * 0.35 && bounds.Bottom > window.Y;
                })
                .OrderBy(element => element.Cached.BoundingRectangle.Top)
                .ThenBy(element => element.Cached.BoundingRectangle.Height)
                .ToArray();
            return regions.Length == 0 ? [root] : regions;
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
        cache.Add(ValuePattern.IsReadOnlyProperty);
        return cache;
    }

    private static UiaElementSnapshot ToSnapshot(AutomationElement element)
    {
        var cached = element.Cached;
        var rectangle = cached.BoundingRectangle;
        var supportsToggle = GetCachedBoolean(element, AutomationElement.IsTogglePatternAvailableProperty);
        var supportsExpandCollapse = GetCachedBoolean(element, AutomationElement.IsExpandCollapsePatternAvailableProperty);
        var supportsInvoke = GetCachedBoolean(element, AutomationElement.IsInvokePatternAvailableProperty);
        // The managed UIA client returns null for newer native roles such as
        // AppBar. Its paired container patterns distinguish it from a button.
        var controlType = cached.ControlType is null && supportsToggle && supportsExpandCollapse && !supportsInvoke
            ? UiaControlType.AppBar
            : MapControlType(cached.ControlType);
        return new UiaElementSnapshot(
            GetElementId(element, cached.AutomationId, rectangle),
            cached.Name,
            new ElementBounds(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height),
            controlType,
            cached.IsEnabled,
            cached.IsOffscreen,
            cached.IsKeyboardFocusable,
            supportsInvoke,
            supportsToggle,
            GetCachedBoolean(element, AutomationElement.IsSelectionItemPatternAvailableProperty),
            supportsExpandCollapse,
            GetCachedBoolean(element, AutomationElement.IsValuePatternAvailableProperty),
            GetCachedBoolean(element, AutomationElement.IsRangeValuePatternAvailableProperty),
            GetCachedBoolean(element, ValuePattern.IsReadOnlyProperty));
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

    private static UiaControlType MapControlType(ControlType? controlType)
    {
        // AppBar was added after the managed ControlType constants. Its bounds
        // enclose the toolbar's buttons; treating it as an unknown toggle creates
        // a phantom edge target and suppresses OCR throughout the whole toolbar.
        // https://learn.microsoft.com/windows/win32/winauto/uiauto-controltype-ids
        if (controlType?.Id == 50040) return UiaControlType.AppBar;
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
        if (controlType == ControlType.Tree) return UiaControlType.Tree;
        if (controlType == ControlType.Header) return UiaControlType.Header;
        if (controlType == ControlType.StatusBar) return UiaControlType.StatusBar;
        return UiaControlType.Unknown;
    }
}
