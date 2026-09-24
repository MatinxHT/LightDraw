using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LightDraw.Browser.Services;
using LightDraw.Browser.Views;
using LightDraw.Desktop.Services;
using LightDraw.Desktop.ViewModels;

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
            var mainView = new MainView();
            mainView.AttachViewModel(new MainWindowViewModel(new BrowserSceneStorageService()));
            singleView.MainView = mainView;
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplyBrowserLabels()
    {
        if (Application.Current?.Resources is not { } resources) return;
        foreach (var key in new[]
        {
            "Common.Pan", "Common.Move", "Common.ResetScene", "Common.FitWindow",
            "Common.Counterclockwise", "Common.Clockwise"
        })
        {
            var label = LocalizationService.Instance.Get(key);
            var separator = label.IndexOf(' ');
            resources[key] = separator >= 0 ? label[(separator + 1)..] : label;
        }
    }
}
