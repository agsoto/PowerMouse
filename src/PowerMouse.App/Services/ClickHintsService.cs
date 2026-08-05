using Microsoft.UI.Dispatching;
using System.Runtime.InteropServices;
using PowerMouse.App.Interop;
using PowerMouse.ClickHints.Detection;
using PowerMouse.ClickHints.Hints;
using PowerMouse.ClickHints.Windows;
using PowerMouse.Core.Engine;
using PowerMouse.Core.Models;

namespace PowerMouse.App.Services;

internal sealed class ClickHintsService : IDisposable
{
    private readonly Lock stateLock = new();
    private readonly HashSet<int> downKeys = [];
    private readonly HashSet<int> suppressedUntilKeyUp = [];
    private readonly PowerMouseSettings settings;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ClickHintDetector detector;
    private readonly HintCodeGenerator codeGenerator = new();
    private readonly HintSequenceMatcher sequenceMatcher = new();
    private readonly ClickHintsOverlayService overlay = new();
    private readonly ClickHintsDismissalMonitor dismissalMonitor;
    private readonly Dictionary<string, IReadOnlyList<DetectedElement>> clusters = new(StringComparer.Ordinal);
    private CancellationTokenSource? detectionCancellation;
    private bool sessionOpen;
    private bool disposed;

    public ClickHintsService(PowerMouseSettings settings, DispatcherQueue dispatcherQueue)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dispatcherQueue);
        this.settings = settings;
        this.dispatcherQueue = dispatcherQueue;
        detector = CreateDetector();
        overlay.HintInvoked += hint => ActivateHint(hint, invertAction: false);
        dismissalMonitor = new ClickHintsDismissalMonitor(
            () => dispatcherQueue.TryEnqueue(Dismiss),
            overlay.IsHintPoint);
    }

    public event Action<string>? Notice;

    public bool ProcessKey(KeyboardStroke stroke)
    {
        if (stroke.IsInjected || disposed)
        {
            return false;
        }

        var key = KeyNormalizer.Normalize(stroke);
        lock (stateLock)
        {
            var wasDown = downKeys.Contains(key);
            if (stroke.IsKeyDown)
            {
                downKeys.Add(key);
            }
            else
            {
                downKeys.Remove(key);
            }

            if (!stroke.IsKeyDown && suppressedUntilKeyUp.Remove(key))
            {
                return true;
            }

            if (stroke.IsKeyDown && !wasDown && MatchesShortcutLocked(key))
            {
                suppressedUntilKeyUp.Add(key);
                var activate = !sessionOpen;
                sessionOpen = activate;
                dispatcherQueue.TryEnqueue(() =>
                {
                    if (activate)
                    {
                        _ = ActivateAsync();
                    }
                    else
                    {
                        DismissCore();
                    }
                });
                return true;
            }

            if (!sessionOpen || !stroke.IsKeyDown || wasDown)
            {
                return false;
            }

            if (key == VirtualKeys.Escape)
            {
                suppressedUntilKeyUp.Add(key);
                sessionOpen = false;
                dispatcherQueue.TryEnqueue(DismissCore);
                return true;
            }

            if (key == VirtualKeys.Back)
            {
                suppressedUntilKeyUp.Add(key);
                dispatcherQueue.TryEnqueue(RemoveLastCharacter);
                return true;
            }

            if (key is >= VirtualKeys.KeyA and <= VirtualKeys.KeyZ)
            {
                suppressedUntilKeyUp.Add(key);
                var invertAction = IsActionModifierDownLocked();
                dispatcherQueue.TryEnqueue(() => AddCharacter((char)key, invertAction));
                return true;
            }

            return false;
        }
    }

    public void Dismiss()
    {
        lock (stateLock)
        {
            sessionOpen = false;
        }

        DismissCore();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Dismiss();
        dismissalMonitor.Dispose();
        overlay.Dispose();
    }

    private static ClickHintDetector CreateDetector()
    {
        var semantic = new UiAutomationElementProvider();
        try
        {
            var recognizer = new WindowsMediaOcrTextRecognizer(new GdiWindowImageCapture());
            return new ClickHintDetector(semantic, new OcrElementDetectionProvider(recognizer));
        }
        catch (Exception exception) when (exception is InvalidOperationException or COMException)
        {
            return new ClickHintDetector(semantic);
        }
    }

    private async Task ActivateAsync()
    {
        detectionCancellation?.Cancel();
        detectionCancellation?.Dispose();
        detectionCancellation = new CancellationTokenSource();
        var cancellation = detectionCancellation;
        var request = ForegroundWindowTarget.TryCreate();
        if (request is null || request.TargetWindow == overlay.WindowHandle)
        {
            Dismiss();
            return;
        }

        try
        {
            var result = await detector.DetectAsync(request, cancellation.Token);
            lock (stateLock)
            {
                if (!sessionOpen || cancellation.IsCancellationRequested)
                {
                    return;
                }
            }

            if (NativeMethods.GetForegroundWindow() != request.TargetWindow)
            {
                Dismiss();
                return;
            }

            if (result.Elements.Count == 0)
            {
                Dismiss();
                Notice?.Invoke("No mouse targets were found in the active window.");
                return;
            }

            ShowInitialHints(result.Elements);
            dismissalMonitor.Start(request.TargetWindow);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Dismissal cancels discovery in progress.
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            Dismiss();
            Notice?.Invoke($"Mouse Hints could not inspect this window: {exception.Message}");
        }
    }

    private void AddCharacter(char character, bool invertAction)
    {
        lock (stateLock)
        {
            if (!sessionOpen)
            {
                return;
            }
        }

        UpdateMatches(sequenceMatcher.Append(character), invertAction);
    }

    private void RemoveLastCharacter()
    {
        lock (stateLock)
        {
            if (!sessionOpen)
            {
                return;
            }
        }

        UpdateMatches(sequenceMatcher.Backspace(), invertAction: false);
    }

    private void UpdateMatches(HintMatchResult result, bool invertAction)
    {
        if (result.Selected is not null)
        {
            ActivateHint(result.Selected, invertAction);
            return;
        }

        if (result.IsDeadEnd)
        {
            Dismiss();
            return;
        }

        overlay.Update(result.Candidates);
    }

    private void ShowInitialHints(IReadOnlyList<DetectedElement> elements)
    {
        clusters.Clear();
        var groups = HintClusterEngine.Cluster(elements);
        var topLevelElements = new List<DetectedElement>(groups.Count);
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            if (!group.IsCluster)
            {
                topLevelElements.Add(group.Elements[0]);
                continue;
            }

            var id = $"cluster:{index}";
            clusters[id] = group.Elements;
            topLevelElements.Add(new DetectedElement(
                id,
                new ElementBounds(group.CenterX - 1, group.CenterY - 1, 2, 2),
                ElementInteraction.None,
                group.Elements[0].Source,
                $"{group.Elements.Count} nearby targets"));
        }

        var hints = codeGenerator.Assign(topLevelElements)
            .Select(hint => clusters.TryGetValue(hint.Element.Id, out var members)
                ? hint with { ClusterSize = members.Count }
                : hint)
            .ToArray();
        sequenceMatcher.Reset(hints);
        overlay.Show(hints);
    }

    private void ExpandCluster(string clusterId)
    {
        lock (stateLock)
        {
            if (!sessionOpen)
            {
                return;
            }
        }

        if (!clusters.TryGetValue(clusterId, out var members))
        {
            return;
        }

        var targets = HintTargetPlacementEngine.Place(members)
            .ToDictionary(placement => placement.Element.Id, placement => placement.ClickTarget);
        var hints = codeGenerator.Assign(members)
            .Select(hint => hint with { LeaderTarget = targets[hint.Element.Id] })
            .ToArray();
        sequenceMatcher.Reset(hints);
        overlay.Show(hints);
    }

    private void ActivateHint(ClickHint hint, bool invertAction)
    {
        if (clusters.ContainsKey(hint.Element.Id))
        {
            ExpandCluster(hint.Element.Id);
            return;
        }

        var target = hint.LeaderTarget ?? hint.Element.Bounds;
        var x = checked((int)Math.Round(target.X + (target.Width / 2)));
        var y = checked((int)Math.Round(target.Y + (target.Height / 2)));
        var action = MouseHintActionResolver.Resolve(settings.MouseHintsAutoClick, invertAction);
        Dismiss();
        try
        {
            if (action == MouseHintAction.Click)
            {
                MouseInputService.ClickAt(x, y);
            }
            else
            {
                MouseInputService.MoveTo(x, y);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Notice?.Invoke(exception.Message);
        }
    }

    private void DismissCore()
    {
        detectionCancellation?.Cancel();
        detectionCancellation?.Dispose();
        detectionCancellation = null;
        dismissalMonitor.Stop();
        overlay.Hide();
        clusters.Clear();
        sequenceMatcher.Reset([]);
    }

    private bool MatchesShortcutLocked(int key)
    {
        var shortcut = settings.MouseHintsShortcut;
        return key == shortcut.VirtualKey &&
               (GetModifiersLocked() & shortcut.Modifiers) == shortcut.Modifiers;
    }

    private bool IsActionModifierDownLocked()
    {
        var modifier = settings.MouseHintsActionModifier;
        return (GetModifiersLocked() & modifier) == modifier;
    }

    private HotkeyModifiers GetModifiersLocked()
    {
        var modifiers = HotkeyModifiers.None;
        if (downKeys.Overlaps([VirtualKeys.Control, VirtualKeys.LeftControl, VirtualKeys.RightControl]))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (downKeys.Overlaps([VirtualKeys.Menu, VirtualKeys.LeftMenu, VirtualKeys.RightMenu]))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (downKeys.Overlaps([VirtualKeys.Shift, VirtualKeys.LeftShift, VirtualKeys.RightShift]))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (downKeys.Overlaps([VirtualKeys.LeftWindows, VirtualKeys.RightWindows]))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        return modifiers;
    }
}
