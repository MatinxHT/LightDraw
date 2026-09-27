using LightDraw.Core.Scene;
using LightDraw.Core.Electromagnetics;

namespace LightDraw.Core.Persistence;

public sealed class MagnetostaticSceneCodec : ISceneCodec<MagnetostaticScene>
{
    public static MagnetostaticSceneCodec Instance { get; } = new();
    public string FileStem => "lightdraw-magnetostatic";
    public MagnetostaticScene CreateEmpty() => MagnetostaticScene.CreateEmpty();
    public MagnetostaticScene Normalize(MagnetostaticScene scene) => ElectromagneticSceneNormalizer.Normalize(scene);
    public MagnetostaticScene Snapshot(MagnetostaticScene scene) => scene with
    {
        Conductors = [.. scene.Conductors],
        VerticalConductors = [.. scene.VerticalConductorElements],
        PlanarLoops = [.. scene.PlanarLoopElements],
        VerticalLoops = [.. scene.VerticalLoopElements]
    };

    public bool ContentEquals(MagnetostaticScene left, MagnetostaticScene right) =>
        ReferenceEquals(left, right) || (left.Name == right.Name &&
        left.Conductors.SequenceEqual(right.Conductors) &&
        left.VerticalConductorElements.SequenceEqual(right.VerticalConductorElements) &&
        left.PlanarLoopElements.SequenceEqual(right.PlanarLoopElements) &&
        left.VerticalLoopElements.SequenceEqual(right.VerticalLoopElements));

    public Task<MagnetostaticScene> LoadAsync(Stream stream, CancellationToken cancellationToken = default) =>
        ElectromagneticSceneSerializer.LoadMagnetostaticAsync(stream, cancellationToken);

    public Task SaveAsync(MagnetostaticScene scene, Stream stream, CancellationToken cancellationToken = default) =>
        ElectromagneticSceneSerializer.SaveAsync(scene, stream, cancellationToken);
}
