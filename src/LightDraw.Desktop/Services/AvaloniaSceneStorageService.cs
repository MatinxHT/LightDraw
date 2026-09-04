using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LightDraw.Core.Persistence;
using LightDraw.Core.Scene;

namespace LightDraw.Desktop.Services;

public sealed class AvaloniaSceneStorageService(Window owner) : ISceneStorageService
{
    public async Task<OpenedScene?> OpenAsync(CancellationToken cancellationToken = default)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationService.Instance.Get("Storage.OpenTitle"),
            AllowMultiple = false,
            FileTypeFilter = [CreateSceneFileType()]
        });

        if (files.Count == 0)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = await files[0].OpenReadAsync();
        var scene = await SceneSerializer.LoadAsync(stream, cancellationToken);
        return new OpenedScene(scene, files[0].Name);
    }

    public async Task<string?> SaveAsync(OpticalScene scene, CancellationToken cancellationToken = default)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LocalizationService.Instance.Get("Storage.SaveTitle"),
            SuggestedFileName = "lightdraw-scene",
            DefaultExtension = "lightdraw.json",
            FileTypeChoices = [CreateSceneFileType()]
        });

        if (file is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        await SceneSerializer.SaveAsync(scene, stream, cancellationToken);
        return file.Name;
    }

    private static FilePickerFileType CreateSceneFileType() => new(
        LocalizationService.Instance.Get("Storage.SceneType"))
    {
        Patterns = ["*.lightdraw.json", "*.json"],
        AppleUniformTypeIdentifiers = ["public.json"],
        MimeTypes = ["application/json"]
    };
}
