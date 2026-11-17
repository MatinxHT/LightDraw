using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LightDraw.Browser.Views;
using LightDraw.Desktop.Services;

namespace LightDraw.Browser;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        LocalizationService.Instance.ApplyResources();
        ApplyBrowserLabels();
        LocalizationService.Instance.LanguageChanged += (_, _) => ApplyBrowserLabels();
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new WorkspaceView();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplyBrowserLabels()
    {
        if (Application.Current?.Resources is not { } resources) return;
        foreach (var key in new[]
        {
            "Common.Pan", "Common.Move", "Common.ResetScene", "Common.FitWindow",
            "Common.Counterclockwise", "Common.Clockwise",
            "Electro.PointCharge", "Electro.ChargedPlate",
            "Magnetic.PlanarConductor", "Magnetic.VerticalConductor",
            "Magnetic.PlanarLoop", "Magnetic.VerticalLoop"
        })
        {
            var label = LocalizationService.Instance.Get(key);
            var separator = label.IndexOf(' ');
            resources[key] = separator >= 0 ? label[(separator + 1)..] : label;
        }
        // The bundled browser font does not contain Unicode subscripts.
        foreach (var key in new[] { "Electro.Law", "Magnetic.Law" })
            resources[key] = LocalizationService.Instance.Get(key).Replace("₀", "0");
    }
}
