using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerMouse.App.Diagnostics;

namespace PowerMouse.App.Pages;

public sealed partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "Unknown";
    }

    private void OnExitClicked(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        App.CurrentApp.Shutdown();
    }

    private void OnOpenLogsClicked(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        try
        {
            Directory.CreateDirectory(Logger.AppLogDirectoryPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = Logger.AppLogDirectoryPath,
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Logger.LogError("The logs folder could not be opened.", exception);
        }
    }
}
