using Avalonia.Controls;
using Avalonia.Styling;

namespace LightDraw.Rendering.Skia;

public abstract class ThemedCanvas : Control
{
    protected ThemedCanvas()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    protected bool IsLightTheme => ActualThemeVariant == ThemeVariant.Light;
}
