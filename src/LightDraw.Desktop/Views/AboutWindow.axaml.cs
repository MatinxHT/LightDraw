using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LightDraw.Desktop.Services;

namespace LightDraw.Desktop.Views;

public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var informationalVersion = typeof(AboutWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var localizer = LocalizationService.Instance;
        var displayVersion = informationalVersion?.Split('+')[0] ?? localizer.Get("About.Unknown");
        VersionText.Text = string.Format(localizer.Get("About.Version"), displayVersion);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
