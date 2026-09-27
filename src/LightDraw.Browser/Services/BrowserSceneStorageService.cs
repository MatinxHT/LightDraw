using LightDraw.Core.Persistence;
using LightDraw.Core.Scene;
using LightDraw.Desktop.Services;

namespace LightDraw.Browser.Services;

public sealed class BrowserSceneStorageService : ISceneStorageService<OpticalScene>
{
    public async Task<OpenedScene<OpticalScene>?> OpenAsync(CancellationToken cancellationToken = default)
    {
        var data = await BrowserFileInterop.PickSceneAsync();
        if (data is null) return null;
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = new MemoryStream(data.Value.Bytes);
        var scene = await SceneSerializer.LoadAsync(stream, cancellationToken);
        return new OpenedScene<OpticalScene>(scene, data.Value.FileName);
    }

    public async Task<string?> SaveAsync(OpticalScene scene, CancellationToken cancellationToken = default)
    {
        await using var stream = new MemoryStream();
        await SceneSerializer.SaveAsync(scene, stream, cancellationToken);
        const string fileName = "lightdraw-scene.lightdraw.json";
        BrowserFileInterop.Download(fileName, "application/json", stream.ToArray());
        return fileName;
    }
}
