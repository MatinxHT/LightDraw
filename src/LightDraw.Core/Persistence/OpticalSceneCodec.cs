using LightDraw.Core.Scene;

namespace LightDraw.Core.Persistence;

public sealed class OpticalSceneCodec : ISceneCodec<OpticalScene>
{
    public static OpticalSceneCodec Instance { get; } = new();
    public string FileStem => "lightdraw-scene";
    public OpticalScene CreateEmpty() => OpticalScene.CreateEmpty();
    public OpticalScene Normalize(OpticalScene scene) => OpticalSceneNormalizer.Normalize(scene);
    public OpticalScene Snapshot(OpticalScene scene) => scene with
    {
        LightSources = [.. scene.LightSources],
        Mirrors = [.. scene.Mirrors],
        ConcaveSphericalMirrors = [.. scene.ConcaveSphericalMirrorElements],
        ConvexSphericalMirrors = [.. scene.ConvexSphericalMirrorElements],
        BeamSplitters = [.. scene.BeamSplitterElements],
        Screens = [.. scene.ScreenElements],
        Apertures = [.. scene.ApertureElements],
        ReflectionGratings = [.. scene.ReflectionGratingElements],
        ConcaveGratings = [.. scene.ConcaveGratingElements],
        Lenses = [.. scene.LensElements],
        Groups = scene.ElementGroups.Select(group => group with { MemberIds = [.. group.MemberIds] }).ToArray()
    };

    public bool ContentEquals(OpticalScene left, OpticalScene right) =>
        ReferenceEquals(left, right) || (left.Name == right.Name &&
        left.LightSources.SequenceEqual(right.LightSources) &&
        left.Mirrors.SequenceEqual(right.Mirrors) &&
        left.ConcaveSphericalMirrorElements.SequenceEqual(right.ConcaveSphericalMirrorElements) &&
        left.ConvexSphericalMirrorElements.SequenceEqual(right.ConvexSphericalMirrorElements) &&
        left.BeamSplitterElements.SequenceEqual(right.BeamSplitterElements) &&
        left.ScreenElements.SequenceEqual(right.ScreenElements) &&
        left.ApertureElements.SequenceEqual(right.ApertureElements) &&
        left.ReflectionGratingElements.SequenceEqual(right.ReflectionGratingElements) &&
        left.ConcaveGratingElements.SequenceEqual(right.ConcaveGratingElements) &&
        left.LensElements.SequenceEqual(right.LensElements) &&
        left.ElementGroups.Length == right.ElementGroups.Length &&
        left.ElementGroups.Zip(right.ElementGroups).All(pair =>
            pair.First.Id == pair.Second.Id && pair.First.Name == pair.Second.Name &&
            pair.First.PrimaryMemberId == pair.Second.PrimaryMemberId &&
            pair.First.MemberIds.SequenceEqual(pair.Second.MemberIds)));

    public Task<OpticalScene> LoadAsync(Stream stream, CancellationToken cancellationToken = default) =>
        SceneSerializer.LoadAsync(stream, cancellationToken);

    public Task SaveAsync(OpticalScene scene, Stream stream, CancellationToken cancellationToken = default) =>
        SceneSerializer.SaveAsync(scene, stream, cancellationToken);
}
