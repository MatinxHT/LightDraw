using LightDraw.Core.Scene;

namespace LightDraw.Desktop.Services;

public interface ISceneStorageService
{
    Task<OpenedScene?> OpenAsync(CancellationToken cancellationToken = default);

    Task<string?> SaveAsync(OpticalScene scene, CancellationToken cancellationToken = default);
}

public sealed record OpenedScene(OpticalScene Scene, string FileName);
