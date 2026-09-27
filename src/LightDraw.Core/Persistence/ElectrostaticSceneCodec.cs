using LightDraw.Core.Scene;
using LightDraw.Core.Electromagnetics;

namespace LightDraw.Core.Persistence;

public sealed class ElectrostaticSceneCodec : ISceneCodec<ElectrostaticScene>
{
    public static ElectrostaticSceneCodec Instance { get; } = new();
    public string FileStem => "lightdraw-electrostatic";
    public ElectrostaticScene CreateEmpty() => ElectrostaticScene.CreateEmpty();
    public ElectrostaticScene Normalize(ElectrostaticScene scene) => ElectromagneticSceneNormalizer.Normalize(scene);
    public ElectrostaticScene Snapshot(ElectrostaticScene scene) => scene with
    {
        Charges = [.. scene.Charges],
        Plates = [.. scene.PlateElements]
    };

    public bool ContentEquals(ElectrostaticScene left, ElectrostaticScene right) =>
        ReferenceEquals(left, right) || (left.Name == right.Name &&
        left.Charges.SequenceEqual(right.Charges) &&
        left.PlateElements.SequenceEqual(right.PlateElements));

    public Task<ElectrostaticScene> LoadAsync(Stream stream, CancellationToken cancellationToken = default) =>
        ElectromagneticSceneSerializer.LoadElectrostaticAsync(stream, cancellationToken);

    public Task SaveAsync(ElectrostaticScene scene, Stream stream, CancellationToken cancellationToken = default) =>
        ElectromagneticSceneSerializer.SaveAsync(scene, stream, cancellationToken);
}
