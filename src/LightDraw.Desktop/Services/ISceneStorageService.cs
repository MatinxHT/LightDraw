namespace LightDraw.Desktop.Services;

public interface ISceneStorageService<T> where T : class
{
    Task<OpenedScene<T>?> OpenAsync(CancellationToken cancellationToken = default);
    Task<string?> SaveAsync(T scene, CancellationToken cancellationToken = default);
}

public sealed record OpenedScene<T>(T Scene, string FileName) where T : class;
