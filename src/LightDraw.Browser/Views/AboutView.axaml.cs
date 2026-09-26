using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LightDraw.Desktop.Services;

namespace LightDraw.Browser.Views;

public sealed partial class AboutView : UserControl
{
    public event EventHandler? CloseRequested;

    public AboutView()
    {
        InitializeComponent();
        var informationalVersion = typeof(AboutView).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var localizer = LocalizationService.Instance;
        var displayVersion = informationalVersion?.Split('+')[0] ?? localizer.Get("About.Unknown");
        VersionText.Text = string.Format(localizer.Get("About.Version"), displayVersion);
    }

    public void FocusCloseButton() => CloseButton.Focus();

    private void OnCloseClicked(object? sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
}
