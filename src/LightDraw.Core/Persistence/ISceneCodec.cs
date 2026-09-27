namespace LightDraw.Core.Persistence;

public interface ISceneCodec<T> where T : class
{
    string FileStem { get; }
    T CreateEmpty();
    T Normalize(T scene);
    T Snapshot(T scene);
    bool ContentEquals(T left, T right);
    Task<T> LoadAsync(Stream stream, CancellationToken cancellationToken = default);
    Task SaveAsync(T scene, Stream stream, CancellationToken cancellationToken = default);
}
