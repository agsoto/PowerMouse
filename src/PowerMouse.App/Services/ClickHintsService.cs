using Microsoft.UI.Dispatching;
using System.Runtime.InteropServices;
using PowerMouse.App.Diagnostics;
using PowerMouse.App.Interop;
using PowerMouse.ClickHints.Detection;
using PowerMouse.ClickHints.Hints;
using PowerMouse.ClickHints.Windows;
using PowerMouse.Core.Engine;
using PowerMouse.Core.Models;

namespace PowerMouse.App.Services;

internal sealed class ClickHintsService : IDisposable
{
    private static readonly System.Text.Json.JsonSerializerOptions InspectionJsonOptions = new() { WriteIndented = true };
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
    private long sessionVersion;
    private bool hintsReady;
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
            QueueDismiss,
            overlay.IsHintPoint);
    }

    public event Action<string>? Notice;

    // Explicit local diagnostic entry point. Normal injected keyboard events
    // remain ignored, and this uses the same session and detection path.
    public void InspectForeground()
    {
        long version;
        lock (stateLock)
        {
            if (disposed || !settings.MouseHintsEnabled) return;
            sessionOpen = true;
            hintsReady = false;
            version = ++sessionVersion;
        }
        dispatcherQueue.TryEnqueue(() =>
        {
            if (IsCurrentSession(version)) _ = ActivateAsync(version, inspect: true);
        });
    }

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

            if (stroke.IsKeyDown && suppressedUntilKeyUp.Contains(key))
            {
                return true;
            }

            if (!settings.MouseHintsEnabled)
            {
                return false;
            }

            if (stroke.IsKeyDown && !wasDown && MatchesShortcutLocked(key))
            {
                suppressedUntilKeyUp.Add(key);
                var activate = !sessionOpen;
                sessionOpen = activate;
                hintsReady = false;
                var version = ++sessionVersion;
                dispatcherQueue.TryEnqueue(() =>
                {
                    if (!IsCurrentSession(version)) return;
                    if (activate)
                    {
                        _ = ActivateAsync(version);
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
                var version = ++sessionVersion;
                dispatcherQueue.TryEnqueue(() =>
                {
                    if (IsCurrentSession(version)) DismissCore();
                });
                return true;
            }

            if (key == VirtualKeys.Back)
            {
                suppressedUntilKeyUp.Add(key);
                if (hintsReady)
                {
                    var version = sessionVersion;
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        if (IsCurrentSession(version)) RemoveLastCharacter();
                    });
                }
                return true;
            }

            if (key is >= VirtualKeys.KeyA and <= VirtualKeys.KeyZ)
            {
                suppressedUntilKeyUp.Add(key);
                var invertAction = IsActionModifierDownLocked();
                if (hintsReady)
                {
                    var version = sessionVersion;
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        if (IsCurrentSession(version)) AddCharacter((char)key, invertAction);
                    });
                }
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
            hintsReady = false;
            sessionVersion++;
        }

        DismissCore();
    }

    public void ApplySettings()
    {
        if (settings.MouseHintsEnabled)
        {
            return;
        }

        lock (stateLock)
        {
            sessionOpen = false;
            hintsReady = false;
            sessionVersion++;
            downKeys.Clear();
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
            Logger.LogWarning($"OCR initialization failed; Mouse Hints will use UI Automation only. {exception.GetType().Name}: {exception.Message}");
            return new ClickHintDetector(semantic);
        }
    }

    private bool IsCurrentSession(long version)
    {
        lock (stateLock) return !disposed && version == sessionVersion;
    }

    private void QueueDismiss()
    {
        long version;
        lock (stateLock) version = sessionVersion;
        dispatcherQueue.TryEnqueue(() =>
        {
            if (IsCurrentSession(version)) Dismiss();
        });
    }

    private async Task ActivateAsync(long version, bool inspect = false)
    {
        DismissCore();
        detectionCancellation = new CancellationTokenSource();
        var cancellation = detectionCancellation;
        var request = ForegroundWindowTarget.TryCreate();
        if (request is null || request.TargetWindow == overlay.WindowHandle)
        {
            Dismiss();
            cancellation.Dispose();
            return;
        }

        try
        {
            // Monitor during discovery too, so a click or window switch cannot
            // leave late labels over a target that has already changed.
            dismissalMonitor.Start(request.TargetWindow);
            var result = await detector.DetectAsync(request, cancellation.Token);
            Logger.LogInfo(
                $"Mouse Hints detected {result.Elements.Count} targets in {result.Elapsed.TotalMilliseconds:0} ms. " +
                $"OCR used={result.UsedOcrFallback}.");
            foreach (var diagnostic in result.Diagnostics)
            {
                Logger.LogWarning($"Mouse Hints detection: {diagnostic}");
            }

            lock (stateLock)
            {
                if (!sessionOpen || version != sessionVersion || disposed || cancellation.IsCancellationRequested)
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

            var displayedHints = ShowInitialHints(result.Elements);
            if (inspect)
            {
                WriteInspection(request, result, displayedHints);
            }
            lock (stateLock)
            {
                hintsReady = sessionOpen && version == sessionVersion && !disposed;
                if (!hintsReady) overlay.Hide();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Dismissal cancels discovery in progress.
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            if (!IsCurrentSession(version) || cancellation.IsCancellationRequested) return;
            Logger.LogError("Mouse Hints could not inspect the active window.", exception);
            Dismiss();
            Notice?.Invoke($"Mouse Hints could not inspect this window: {exception.Message}");
        }
        finally
        {
            if (ReferenceEquals(detectionCancellation, cancellation)) detectionCancellation = null;
            cancellation.Dispose();
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

    private ClickHint[] ShowInitialHints(IReadOnlyList<DetectedElement> elements)
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
        return hints;
    }

    private static void WriteInspection(DetectionRequest request, DetectionResult result, IReadOnlyList<ClickHint> hints)
    {
        var folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PowerMouse", "Diagnostics");
        System.IO.Directory.CreateDirectory(folder);
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            CapturedAt = DateTimeOffset.UtcNow,
            Window = (long)request.TargetWindow,
            request.TargetBounds,
            result.Elapsed,
            result.Diagnostics,
            result.Elements,
            Hints = hints,
        }, InspectionJsonOptions);
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "hints.json"), json);
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
        lock (stateLock)
        {
            if (!sessionOpen || !hintsReady) return;
        }

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
            Logger.LogError("Mouse Hints could not invoke the selected target.", exception);
            Notice?.Invoke(exception.Message);
        }
    }

    private void DismissCore()
    {
        detectionCancellation?.Cancel();
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
