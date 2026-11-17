using LightDraw.Core.Persistence;
using LightDraw.Desktop.Services;

namespace LightDraw.Browser.Services;

public sealed class BrowserSceneStorageService<T>(ISceneCodec<T> codec) : ISceneStorageService<T> where T : class
{
    public async Task<OpenedScene<T>?> OpenAsync(CancellationToken cancellationToken = default)
    {
        var data = await BrowserFileInterop.PickSceneAsync();
        if (data is null) return null;
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = new MemoryStream(data.Value.Bytes);
        var scene = await codec.LoadAsync(stream, cancellationToken);
        return new OpenedScene<T>(scene, data.Value.FileName);
    }

    public async Task<string?> SaveAsync(T scene, CancellationToken cancellationToken = default)
    {
        await using var stream = new MemoryStream();
        await codec.SaveAsync(scene, stream, cancellationToken);
        var fileName = $"{codec.FileStem}.lightdraw.json";
        BrowserFileInterop.Download(fileName, "application/json", stream.ToArray());
        return fileName;
    }
}
