using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PowerMouse.App.Pages;

public sealed partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    }

    private void OnExitClicked(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        App.CurrentApp.Shutdown();
    }
}
