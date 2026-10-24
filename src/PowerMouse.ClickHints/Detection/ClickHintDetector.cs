using System.Diagnostics;

namespace PowerMouse.ClickHints.Detection;

public sealed class ClickHintDetector
{
    private readonly IElementDetectionProvider semanticProvider;
    private readonly IElementDetectionProvider? ocrProvider;

    public ClickHintDetector(
        IElementDetectionProvider semanticProvider,
        IElementDetectionProvider? ocrProvider = null)
    {
        ArgumentNullException.ThrowIfNull(semanticProvider);
        this.semanticProvider = semanticProvider;
        this.ocrProvider = ocrProvider;
    }

    public async ValueTask<DetectionResult> DetectAsync(
        DetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();
        var diagnostics = new List<string>();
        var semanticTask = RunProviderAsync(
            semanticProvider,
            request,
            request.Options.SemanticTimeout,
            diagnostics,
            cancellationToken).AsTask();
        // UIA is sometimes only partially implemented by custom and web-based
        // interfaces. Start OCR with UIA so a healthy-but-incomplete tree does
        // not suppress the visual fallback merely because it found a few
        // unrelated targets. The two independent operations overlap, so this
        // does not add their timeout budgets together.
        var canUseOcr = request.Options.EnableOcrFallback && ocrProvider is not null;
        var runOcrInParallel = canUseOcr && request.Options.EnableOcrAugmentation;
        var ocrTask = runOcrInParallel
            ? RunProviderAsync(
                ocrProvider!,
                request,
                request.Options.OcrTimeout,
                diagnostics,
                cancellationToken).AsTask()
            : null;

        if (ocrTask is not null)
        {
            // Observe both operations on cancellation; neither may outlive a
            // session as an unobserved fault or publish a partial result.
            await Task.WhenAll(semanticTask, ocrTask).ConfigureAwait(false);
        }

        var semantic = await semanticTask.ConfigureAwait(false);

        var semanticElements = DetectedElementDeduplicator
            .Deduplicate(Sanitize(semantic.Elements, request).ToArray())
            .Take(request.Options.MaximumTargets)
            .ToArray();
        var shouldUseOcr = canUseOcr &&
                           (request.Options.EnableOcrAugmentation ||
                            semanticElements.Length < request.Options.MinimumSemanticTargets);
        IReadOnlyList<DetectedElement> elements = semanticElements;
        if (shouldUseOcr)
        {
            ocrTask ??= RunProviderAsync(
                ocrProvider!,
                request,
                request.Options.OcrTimeout,
                diagnostics,
                cancellationToken).AsTask();
            var ocr = await ocrTask.ConfigureAwait(false);
            elements = Merge(semanticElements, Sanitize(ocr.Elements, request), request.Options);
        }

        cancellationToken.ThrowIfCancellationRequested();
        stopwatch.Stop();
        return new DetectionResult(elements, shouldUseOcr, stopwatch.Elapsed, diagnostics);
    }

    private static async ValueTask<DetectionProviderResult> RunProviderAsync(
        IElementDetectionProvider provider,
        DetectionRequest request,
        TimeSpan timeout,
        List<string> diagnostics,
        CancellationToken cancellationToken)
    {
        using var providerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        DetectionProviderResult? partial = null;
        try
        {
            var operation = provider is IIncrementalElementDetectionProvider incremental
                ? incremental.DetectAsync(request,
                    elements => Volatile.Write(ref partial, DetectionProviderResult.Success(elements)),
                    providerCancellation.Token)
                : provider.DetectAsync(request, providerCancellation.Token);
            var result = await operation
                .AsTask()
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                AddDiagnostic(diagnostics, $"{provider.Name}: {result.Error}");
                return Volatile.Read(ref partial) ?? result;
            }

            return result;
        }
        catch (TimeoutException)
        {
            // WaitAsync limits the caller's wait, but does not stop the provider.
            providerCancellation.Cancel();
            var retained = Volatile.Read(ref partial);
            AddDiagnostic(diagnostics, $"{provider.Name}: exceeded the {timeout.TotalMilliseconds:0} ms budget; retained {retained?.Elements.Count ?? 0} targets.");
            return retained ?? DetectionProviderResult.Failure("Timed out.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // WaitAsync can observe parent cancellation before the linked
            // source's callback runs. Cancel explicitly before disposing it.
            providerCancellation.Cancel();
            throw;
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            AddDiagnostic(diagnostics, $"{provider.Name}: {exception.GetType().Name}: {exception.Message}");
            return Volatile.Read(ref partial) ?? DetectionProviderResult.Failure("Provider failed.");
        }
    }

    private static void AddDiagnostic(List<string> diagnostics, string message)
    {
        lock (diagnostics)
        {
            diagnostics.Add(message);
        }
    }

    private static IEnumerable<DetectedElement> Sanitize(
        IEnumerable<DetectedElement> elements,
        DetectionRequest request) =>
        elements
            .Where(element => Intersects(element.Bounds, request.TargetBounds))
            .Select(element => Clip(element, request.TargetBounds))
            .DistinctBy(element => (element.Source, element.Id))
            .OrderBy(element => element.Bounds.Y)
            .ThenBy(element => element.Bounds.X);

    private static DetectedElement Clip(DetectedElement element, ElementBounds window)
    {
        static ElementBounds Intersection(ElementBounds bounds, ElementBounds clip)
        {
            var x = Math.Max(bounds.X, clip.X);
            var y = Math.Max(bounds.Y, clip.Y);
            return new ElementBounds(x, y, Math.Min(bounds.Right, clip.Right) - x,
                Math.Min(bounds.Bottom, clip.Bottom) - y);
        }

        var bounds = Intersection(element.Bounds, window);
        var visualBounds = Intersection(element.VisualBounds, window);
        return new DetectedElement(element.Id, bounds, element.Interaction, element.Source,
            element.Name, element.Confidence, visualBounds.IsEmpty ? bounds : visualBounds, element.ControlType);
    }

    private static DetectedElement[] Merge(
        IReadOnlyList<DetectedElement> semantic,
        IEnumerable<DetectedElement> ocr,
        DetectionOptions options)
    {
        var merged = new List<DetectedElement>(semantic);
        foreach (var candidate in ocr.OrderByDescending(element => element.Confidence))
        {
            var centerX = candidate.Bounds.X + (candidate.Bounds.Width / 2);
            var centerY = candidate.Bounds.Y + (candidate.Bounds.Height / 2);
            var duplicate = merged.Any(existing =>
                existing.VisualBounds.Contains(centerX, centerY) ||
                existing.VisualBounds.IntersectionOverUnion(candidate.VisualBounds) >= options.DuplicateOverlapThreshold);
            if (!duplicate)
            {
                merged.Add(candidate);
            }
        }

        // Reserve capacity for semantic controls before adding approximate text.
        return merged
            .Take(options.MaximumTargets)
            .OrderBy(element => element.Bounds.Y)
            .ThenBy(element => element.Bounds.X)
            .ToArray();
    }

    private static bool Intersects(ElementBounds left, ElementBounds right) =>
        left.X < right.Right && left.Right > right.X && left.Y < right.Bottom && left.Bottom > right.Y;
}
