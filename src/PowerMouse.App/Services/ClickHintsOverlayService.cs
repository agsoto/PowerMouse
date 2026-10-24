using System.ComponentModel;
using System.Runtime.InteropServices;
using PowerMouse.App.Interop;
using PowerMouse.ClickHints.Detection;
using PowerMouse.ClickHints.Hints;

namespace PowerMouse.App.Services;

internal sealed class ClickHintsOverlayService : IDisposable
{
    private const string WindowClassName = "PowerMouse.ClickHintsOverlay";
    private const uint TransparentColor = 0x00FF00FF;
    private const uint BadgeColor = 0x00F9F9F9;
    private const uint BorderColor = 0x00C06700;
    private const uint TextColor = 0x001B1B1B;
    private const uint ClusterTextColor = 0x00FFFFFF;
    private const uint ShadowColor = 0x00909090;

    private readonly NativeMethods.WindowProcedure windowProcedure;
    private readonly nint module;
    private IReadOnlyList<ClickHint> hints = [];
    private IReadOnlyList<HintHitTarget> hitTargets = [];
    private nint window;
    private bool disposed;
    private int virtualX;
    private int virtualY;

    public ClickHintsOverlayService()
    {
        windowProcedure = WindowCallback;
        module = NativeMethods.GetModuleHandle(null);
        var windowClass = new NativeMethods.WindowClassEx
        {
            Size = checked((uint)Marshal.SizeOf<NativeMethods.WindowClassEx>()),
            Style = NativeMethods.ClassHorizontalRedraw | NativeMethods.ClassVerticalRedraw,
            WindowProcedure = windowProcedure,
            Instance = module,
            ClassName = WindowClassName,
        };

        if (NativeMethods.RegisterClassEx(in windowClass) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the Mouse Hints overlay window.");
        }

        window = NativeMethods.CreateWindowEx(
            NativeMethods.WindowExTopMost |
            NativeMethods.WindowExToolWindow |
            NativeMethods.WindowExLayered |
            NativeMethods.WindowExNoActivate,
            WindowClassName,
            string.Empty,
            NativeMethods.WindowPopup,
            0,
            0,
            1,
            1,
            0,
            0,
            module,
            0);

        if (window == 0)
        {
            var error = Marshal.GetLastWin32Error();
            Dispose();
            throw new Win32Exception(error, "Could not create the Mouse Hints overlay window.");
        }
    }

    public nint WindowHandle => window;

    public event Action<ClickHint>? HintInvoked;

    public bool IsHintPoint(int screenX, int screenY) =>
        hitTargets.Any(target => target.Bounds.Contains(screenX, screenY));

    public void Show(IReadOnlyList<ClickHint> value)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(value);
        hints = value;
        virtualX = NativeMethods.GetSystemMetrics(NativeMethods.SystemMetricVirtualScreenX);
        virtualY = NativeMethods.GetSystemMetrics(NativeMethods.SystemMetricVirtualScreenY);
        var width = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SystemMetricVirtualScreenWidth));
        var height = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SystemMetricVirtualScreenHeight));
        NativeMethods.SetWindowPos(
            window,
            NativeMethods.WindowTopMost,
            virtualX,
            virtualY,
            width,
            height,
            NativeMethods.SetWindowPositionNoActivate);
        Paint(window);
        NativeMethods.ShowWindow(window, NativeMethods.ShowWindowNoActivate);
    }

    public void Update(IReadOnlyList<ClickHint> value)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(value);
        hints = value;
        Paint(window);
    }

    public void Hide()
    {
        hints = [];
        hitTargets = [];
        if (window != 0)
        {
            NativeMethods.ShowWindow(window, NativeMethods.ShowWindowHide);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (window != 0)
        {
            NativeMethods.DestroyWindow(window);
            window = 0;
        }

        NativeMethods.UnregisterClass(WindowClassName, module);
    }

    private nint WindowCallback(nint callbackWindow, uint message, nuint wParam, nint lParam)
    {
        _ = lParam;
        if (message == NativeMethods.WmNonClientHitTest)
        {
            return NativeMethods.GetCursorPos(out var point) && IsHintPoint(point.X, point.Y)
                ? NativeMethods.HitTestClient
                : NativeMethods.HitTestTransparent;
        }

        if (message == NativeMethods.WmLeftButtonDown)
        {
            if (NativeMethods.GetCursorPos(out var point))
            {
                var target = hitTargets.FirstOrDefault(value => value.Bounds.Contains(point.X, point.Y));
                if (target is not null)
                {
                    HintInvoked?.Invoke(target.Hint);
                }
            }

            return 0;
        }

        if (message == NativeMethods.WmEraseBackground)
        {
            return 1;
        }

        if (message == NativeMethods.WmPaint)
        {
            // The compositor retains the complete frame; exposure must not
            // repeat detection, layout, or incremental drawing.
            NativeMethods.ValidateRect(callbackWindow, 0);
            return 0;
        }

        return NativeMethods.DefWindowProc(callbackWindow, message, wParam, lParam);
    }

    private void Paint(nint targetWindow)
    {
        if (!NativeMethods.GetClientRect(targetWindow, out var client))
        {
            return;
        }

        using var frame = new LayeredWindowFrame(Math.Max(1, client.Right), Math.Max(1, client.Bottom));
        var deviceContext = frame.DeviceContext;

        var backgroundBrush = NativeMethods.CreateSolidBrush(TransparentColor);
        var badgeBrush = NativeMethods.CreateSolidBrush(BadgeColor);
        var clusterBrush = NativeMethods.CreateSolidBrush(BorderColor);
        var shadowBrush = NativeMethods.CreateSolidBrush(ShadowColor);
        var borderPen = NativeMethods.CreatePen(NativeMethods.PenStyleSolid, 1, BorderColor);
        var font = CreateFont();
        try
        {
            _ = NativeMethods.FillRect(deviceContext, in client, backgroundBrush);

            var oldFont = NativeMethods.SelectObject(deviceContext, font);
            var oldPen = NativeMethods.SelectObject(deviceContext, borderPen);
            var oldBrush = NativeMethods.SelectObject(deviceContext, badgeBrush);
            _ = NativeMethods.SetBkMode(deviceContext, NativeMethods.TransparentBackgroundMode);
            _ = NativeMethods.SetTextColor(deviceContext, TextColor);

            var dpi = NativeMethods.GetDpiForWindow(targetWindow);
            var scale = dpi == 0 ? 1d : dpi / 96d;
            var horizontalPadding = checked((int)Math.Round(7 * scale));
            var badgeHeight = checked((int)Math.Round(24 * scale));
            var radius = checked((int)Math.Round(8 * scale));
            var measuredHints = new List<MeasuredHint>(hints.Count);
            foreach (var hint in hints)
            {
                var text = hint.ClusterSize > 1 ? $"{hint.Keys} +{hint.ClusterSize}" : hint.Keys;
                if (!NativeMethods.GetTextExtentPoint32(
                        deviceContext,
                        text,
                        text.Length,
                        out var textSize))
                {
                    continue;
                }

                var badgeWidth = Math.Max(badgeHeight, textSize.Width + (horizontalPadding * 2));
                measuredHints.Add(new MeasuredHint(hint, text, badgeWidth, badgeHeight));
            }

            var layoutContainer = new ElementBounds(
                2,
                2,
                Math.Max(1, client.Right - 5),
                Math.Max(1, client.Bottom - 6));
            var layoutItems = measuredHints.Select((hint, index) => new HintLayoutItem(
                index,
                hint.Hint.Element.Bounds.X + (hint.Hint.Element.Bounds.Width / 2) - virtualX - (hint.Width / 2d),
                hint.Hint.Element.Bounds.Y + (hint.Hint.Element.Bounds.Height / 2) - virtualY - (hint.Height / 2d),
                hint.Width,
                hint.Height)).ToArray();
            var expanded = measuredHints.Any(hint => hint.Hint.LeaderTarget is not null);
            var layout = expanded
                ? HintLayoutEngine.Layout(layoutItems, layoutContainer)
                : layoutItems.Select(item => new PositionedHintLayoutItem(
                    item.Index,
                    new ElementBounds(
                        Math.Clamp(item.PreferredX, layoutContainer.X, layoutContainer.Right - item.Width),
                        Math.Clamp(item.PreferredY, layoutContainer.Y, layoutContainer.Bottom - item.Height),
                        item.Width,
                        item.Height))).ToArray();

            var renderedTargets = new List<HintHitTarget>(layout.Count);

            foreach (var positioned in layout)
            {
                var measured = measuredHints[positioned.Index];
                var hint = measured.Hint;
                var badgeWidth = measured.Width;
                var x = checked((int)Math.Round(positioned.Bounds.X));
                var y = checked((int)Math.Round(positioned.Bounds.Y));

                if (hint.LeaderTarget is { } leaderTarget)
                {
                    var targetX = checked((int)Math.Round(leaderTarget.X + (leaderTarget.Width / 2) - virtualX));
                    var targetY = checked((int)Math.Round(leaderTarget.Y + (leaderTarget.Height / 2) - virtualY));
                    DrawLeader(deviceContext, x + (badgeWidth / 2), y + (badgeHeight / 2), targetX, targetY);
                }

                NativeMethods.SelectObject(deviceContext, NativeMethods.GetStockObject(NativeMethods.NullPen));
                NativeMethods.SelectObject(deviceContext, shadowBrush);
                NativeMethods.RoundRect(
                    deviceContext,
                    x + 1,
                    y + 2,
                    x + badgeWidth + 1,
                    y + badgeHeight + 2,
                    radius,
                    radius);

                NativeMethods.SelectObject(deviceContext, borderPen);
                NativeMethods.SelectObject(deviceContext, hint.ClusterSize > 1 ? clusterBrush : badgeBrush);
                NativeMethods.RoundRect(
                    deviceContext,
                    x,
                    y,
                    x + badgeWidth,
                    y + badgeHeight,
                    radius,
                    radius);

                var textRectangle = new NativeMethods.NativeRectangle
                {
                    Left = x,
                    Top = y,
                    Right = x + badgeWidth,
                    Bottom = y + badgeHeight,
                };
                _ = NativeMethods.SetTextColor(
                    deviceContext,
                    hint.ClusterSize > 1 ? ClusterTextColor : TextColor);
                _ = NativeMethods.DrawText(
                    deviceContext,
                    measured.Text,
                    measured.Text.Length,
                    ref textRectangle,
                    NativeMethods.DrawTextCenter |
                    NativeMethods.DrawTextVerticalCenter |
                    NativeMethods.DrawTextSingleLine |
                    NativeMethods.DrawTextNoPrefix);
                renderedTargets.Add(new HintHitTarget(
                    hint,
                    new ElementBounds(x + virtualX, y + virtualY, badgeWidth, badgeHeight)));
            }

            NativeMethods.SelectObject(deviceContext, oldBrush);
            NativeMethods.SelectObject(deviceContext, oldPen);
            NativeMethods.SelectObject(deviceContext, oldFont);
            frame.Present(targetWindow, virtualX, virtualY, TransparentColor);
            hitTargets = renderedTargets;
        }
        finally
        {
            NativeMethods.DeleteObject(font);
            NativeMethods.DeleteObject(borderPen);
            NativeMethods.DeleteObject(shadowBrush);
            NativeMethods.DeleteObject(clusterBrush);
            NativeMethods.DeleteObject(badgeBrush);
            NativeMethods.DeleteObject(backgroundBrush);
        }
    }

    private static void DrawLeader(nint deviceContext, int startX, int startY, int targetX, int targetY)
    {
        var deltaX = targetX - startX;
        var deltaY = targetY - startY;
        var length = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (length < 6)
        {
            return;
        }

        _ = NativeMethods.MoveToEx(deviceContext, startX, startY, 0);
        _ = NativeMethods.LineTo(deviceContext, targetX, targetY);

        var directionX = deltaX / length;
        var directionY = deltaY / length;
        var baseX = targetX - (directionX * 8);
        var baseY = targetY - (directionY * 8);
        var perpendicularX = -directionY * 4;
        var perpendicularY = directionX * 4;
        _ = NativeMethods.MoveToEx(deviceContext, targetX, targetY, 0);
        _ = NativeMethods.LineTo(
            deviceContext,
            checked((int)Math.Round(baseX + perpendicularX)),
            checked((int)Math.Round(baseY + perpendicularY)));
        _ = NativeMethods.MoveToEx(deviceContext, targetX, targetY, 0);
        _ = NativeMethods.LineTo(
            deviceContext,
            checked((int)Math.Round(baseX - perpendicularX)),
            checked((int)Math.Round(baseY - perpendicularY)));
    }

    private nint CreateFont()
    {
        var dpi = window == 0 ? 96u : NativeMethods.GetDpiForWindow(window);
        var scale = dpi == 0 ? 1d : dpi / 96d;
        return NativeMethods.CreateFont(
            -checked((int)Math.Round(12 * scale)),
            0,
            0,
            0,
            NativeMethods.FontWeightSemiBold,
            0,
            0,
            0,
            1,
            0,
            0,
            5,
            0,
            "Segoe UI Variable Text");
    }

    private readonly record struct MeasuredHint(ClickHint Hint, string Text, int Width, int Height);

    private sealed record HintHitTarget(ClickHint Hint, ElementBounds Bounds);
}
