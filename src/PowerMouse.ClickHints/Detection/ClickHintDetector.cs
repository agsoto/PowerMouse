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
        var semantic = await RunProviderAsync(
            semanticProvider,
            request,
            request.Options.SemanticTimeout,
            diagnostics,
            cancellationToken).ConfigureAwait(false);

        var semanticElements = DetectedElementDeduplicator
            .Deduplicate(Sanitize(semantic.Elements, request).ToArray())
            .Take(request.Options.MaximumTargets)
            .ToArray();
        var shouldUseOcr = request.Options.EnableOcrFallback &&
                           ocrProvider is not null &&
                           semanticElements.Length < request.Options.MinimumSemanticTargets;

        IReadOnlyList<DetectedElement> elements = semanticElements;
        if (shouldUseOcr)
        {
            var ocr = await RunProviderAsync(
                ocrProvider!,
                request,
                request.Options.OcrTimeout,
                diagnostics,
                cancellationToken).ConfigureAwait(false);
            elements = Merge(semanticElements, Sanitize(ocr.Elements, request), request.Options);
        }

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
        try
        {
            var result = await provider.DetectAsync(request, cancellationToken)
                .AsTask()
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                diagnostics.Add($"{provider.Name}: {result.Error}");
            }

            return result;
        }
        catch (TimeoutException)
        {
            diagnostics.Add($"{provider.Name}: exceeded the {timeout.TotalMilliseconds:0} ms budget.");
            return DetectionProviderResult.Failure("Timed out.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            diagnostics.Add($"{provider.Name}: {exception.GetType().Name}: {exception.Message}");
            return DetectionProviderResult.Failure("Provider failed.");
        }
    }

    private static IEnumerable<DetectedElement> Sanitize(
        IEnumerable<DetectedElement> elements,
        DetectionRequest request) =>
        elements
            .Where(element => Intersects(element.Bounds, request.TargetBounds))
            .DistinctBy(element => (element.Source, element.Id))
            .OrderBy(element => element.Bounds.Y)
            .ThenBy(element => element.Bounds.X);

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
                existing.Bounds.Contains(centerX, centerY) ||
                existing.Bounds.IntersectionOverUnion(candidate.Bounds) >= options.DuplicateOverlapThreshold);
            if (!duplicate)
            {
                merged.Add(candidate);
            }
        }

        return merged
            .OrderBy(element => element.Bounds.Y)
            .ThenBy(element => element.Bounds.X)
            .Take(options.MaximumTargets)
            .ToArray();
    }

    private static bool Intersects(ElementBounds left, ElementBounds right) =>
        left.X < right.Right && left.Right > right.X && left.Y < right.Bottom && left.Bottom > right.Y;
}
