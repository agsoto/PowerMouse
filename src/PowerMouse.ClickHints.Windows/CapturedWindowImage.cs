using Windows.Graphics.Imaging;
using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Windows;

public sealed class CapturedWindowImage : IDisposable
{
    public CapturedWindowImage(SoftwareBitmap bitmap, ElementBounds screenBounds)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (screenBounds.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(screenBounds));
        }

        Bitmap = bitmap;
        ScreenBounds = screenBounds;
    }

    public SoftwareBitmap Bitmap { get; }

    public ElementBounds ScreenBounds { get; }

    public void Dispose() => Bitmap.Dispose();
}
