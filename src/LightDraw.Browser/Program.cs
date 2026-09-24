using Avalonia;
using Avalonia.Browser;
using Avalonia.Media;
using LightDraw.Browser.Services;

namespace LightDraw.Browser;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        await BrowserFileInterop.InitializeAsync();
        await AppBuilder.Configure<App>()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://LightDraw.Browser/Assets/Fonts#Noto Sans CJK SC"
            })
            .StartBrowserAppAsync("out");
    }
}
