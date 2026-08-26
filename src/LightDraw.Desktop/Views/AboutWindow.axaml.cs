using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LightDraw.Desktop.Views;

public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var informationalVersion = typeof(AboutWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var displayVersion = informationalVersion?.Split('+')[0] ?? "未知";
        VersionText.Text = $"版本 {displayVersion}";
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
