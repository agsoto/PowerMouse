using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Windows;

public interface IWindowImageCapture
{
    ValueTask<CapturedWindowImage> CaptureAsync(
        DetectionRequest request,
        CancellationToken cancellationToken = default);
}
