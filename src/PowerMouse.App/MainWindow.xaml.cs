using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PowerMouse.App.Interop;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace PowerMouse.App;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidthDips = 1080;
    private const int DefaultHeightDips = 820;
    private const int WorkAreaMarginDips = 48;

    private readonly AppWindow appWindow;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        try
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        }
        catch (COMException)
        {
            // The normal theme background remains usable when Mica is unavailable.
        }

        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        appWindow = AppWindow.GetFromWindowId(windowId);
        SizeAndCenterWindow(windowHandle, windowId);
        appWindow.Closing += OnWindowClosing;

        AppNavigation.SelectedItem = AppNavigation.MenuItems[0];
    }

    internal void ShowSettings()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            appWindow.Show();
            Activate();
        });
    }

    internal void HideSettings() => appWindow.Hide();

    private void SizeAndCenterWindow(nint windowHandle, WindowId windowId)
    {
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var dpi = NativeMethods.GetDpiForWindow(windowHandle);
        var scale = dpi == 0 ? 1d : dpi / 96d;
        var margin = checked((int)Math.Round(WorkAreaMarginDips * scale));
        var width = Math.Min(
            checked((int)Math.Round(DefaultWidthDips * scale)),
            Math.Max(1, workArea.Width - margin));
        var height = Math.Min(
            checked((int)Math.Round(DefaultHeightDips * scale)),
            Math.Max(1, workArea.Height - margin));
        var x = workArea.X + ((workArea.Width - width) / 2);
        var y = workArea.Y + ((workArea.Height - height) / 2);

        appWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    internal void SetEnabledStatus(bool enabled)
    {
        StatusText.Text = enabled ? "On" : "Off";
        StatusDot.Fill = enabled
            ? new SolidColorBrush(ColorHelper.FromArgb(255, 0, 120, 212))
            : new SolidColorBrush(Colors.Gray);
        SettingsPage.SetEnabledStatus(enabled);
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        _ = sender;
        var tag = (args.SelectedItemContainer?.Tag as string) ?? "settings";
        SettingsPage.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        _ = sender;
        if (!App.CurrentApp.IsShuttingDown)
        {
            args.Cancel = true;
            HideSettings();
        }
    }
}
