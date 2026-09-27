using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LightDraw.Core.Persistence;

namespace LightDraw.Desktop.Services;

public sealed class AvaloniaSceneStorageService<T>(Window owner, ISceneCodec<T> codec) : ISceneStorageService<T> where T : class
{
    public async Task<OpenedScene<T>?> OpenAsync(CancellationToken cancellationToken = default)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationService.Instance.Get("Storage.OpenTitle"),
            AllowMultiple = false,
            FileTypeFilter = [CreateSceneFileType()]
        });
        if (files.Count == 0) return null;
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = await files[0].OpenReadAsync();
        return new(await codec.LoadAsync(stream, cancellationToken), files[0].Name);
    }

    public async Task<string?> SaveAsync(T scene, CancellationToken cancellationToken = default)
    {
        // Finish validation/serialization before touching an existing file.
        using var buffer = new MemoryStream();
        await codec.SaveAsync(scene, buffer, cancellationToken);
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LocalizationService.Instance.Get("Storage.SaveTitle"),
            SuggestedFileName = codec.FileStem,
            DefaultExtension = "lightdraw.json",
            FileTypeChoices = [CreateSceneFileType()]
        });
        if (file is null) return null;
        cancellationToken.ThrowIfCancellationRequested();
        if (file.TryGetLocalPath() is { } path)
        {
            await AtomicSceneFile.WriteAsync(path, buffer.ToArray(), cancellationToken);
        }
        else
        {
            await using var stream = await file.OpenWriteAsync();
            buffer.Position = 0;
            stream.SetLength(0);
            await buffer.CopyToAsync(stream, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        return file.Name;
    }

    private static FilePickerFileType CreateSceneFileType() => new(LocalizationService.Instance.Get("Storage.SceneType"))
    {
        Patterns = ["*.lightdraw.json", "*.json"],
        AppleUniformTypeIdentifiers = ["public.json"],
        MimeTypes = ["application/json"]
    };
}
