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
            Title = "打开 LightDraw 场景",
            AllowMultiple = false,
            FileTypeFilter = [SceneFileType]
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
            Title = "保存 LightDraw 场景",
            SuggestedFileName = "lightdraw-scene",
            DefaultExtension = "lightdraw.json",
            FileTypeChoices = [SceneFileType]
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

    private static FilePickerFileType SceneFileType { get; } = new("LightDraw 场景")
    {
        Patterns = ["*.lightdraw.json", "*.json"],
        AppleUniformTypeIdentifiers = ["public.json"],
        MimeTypes = ["application/json"]
    };
}
