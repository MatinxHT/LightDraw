using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;

namespace LightDraw.Rendering.Skia.Optics;

internal sealed partial class SceneEditor
{
    public void DeleteItemAt(Vector2D world)
    {
        var bestDistance = 12 / _zoom;
        var itemKind = SceneItemKind.None;
        var itemIndex = -1;

        void Consider(double distance, SceneItemKind kind, int index)
        {
            if (distance > bestDistance)
            {
                return;
            }

            bestDistance = distance;
            itemKind = kind;
            itemIndex = index;
        }

        for (var index = 0; index < _scene.LightSources.Length; index++)
        {
            var source = _scene.LightSources[index];
            var distance = source.Kind == LightSourceKind.ParallelLine && source.End is { } end
                ? DistanceToSegment(world, source.Position, end)
                : (world - source.Position).Length;
            Consider(distance, SceneItemKind.LightSource, index);
        }

        for (var index = 0; index < _scene.Mirrors.Length; index++)
        {
            var mirror = _scene.Mirrors[index];
            Consider(DistanceToSegment(world, mirror.Start, mirror.End), SceneItemKind.Mirror, index);
        }

        for (var index = 0; index < _scene.ConcaveSphericalMirrorElements.Length; index++)
        {
            var mirror = _scene.ConcaveSphericalMirrorElements[index];
            Consider(DistanceToArc(world, mirror), SceneItemKind.ConcaveSphericalMirror, index);
        }

        for (var index = 0; index < _scene.ConvexSphericalMirrorElements.Length; index++)
        {
            var mirror = _scene.ConvexSphericalMirrorElements[index];
            Consider(DistanceToArc(world, mirror.Vertex, mirror.CenterOfCurvature,
                mirror.ArcAngleDegrees), SceneItemKind.ConvexSphericalMirror, index);
        }

        for (var index = 0; index < _scene.BeamSplitterElements.Length; index++)
        {
            var beamSplitter = _scene.BeamSplitterElements[index];
            Consider(DistanceToSegment(world, beamSplitter.Start, beamSplitter.End),
                SceneItemKind.BeamSplitter, index);
        }

        for (var index = 0; index < _scene.ScreenElements.Length; index++)
        {
            var screen = _scene.ScreenElements[index];
            Consider(DistanceToSegment(world, screen.Start, screen.End), SceneItemKind.Screen, index);
        }

        for (var index = 0; index < _scene.ApertureElements.Length; index++)
        {
            var aperture = _scene.ApertureElements[index];
            Consider(DistanceToSegment(world, aperture.Start, aperture.End), SceneItemKind.Aperture, index);
        }

        for (var index = 0; index < _scene.ReflectionGratingElements.Length; index++)
        {
            var grating = _scene.ReflectionGratingElements[index];
            Consider(DistanceToSegment(world, grating.Start, grating.End),
                SceneItemKind.ReflectionGrating, index);
        }

        for (var index = 0; index < _scene.ConcaveGratingElements.Length; index++)
        {
            var grating = _scene.ConcaveGratingElements[index];
            Consider(DistanceToArc(world, grating.Vertex, grating.CenterOfCurvature,
                grating.ArcAngleDegrees), SceneItemKind.ConcaveGrating, index);
        }

        for (var index = 0; index < _scene.LensElements.Length; index++)
        {
            var lens = _scene.LensElements[index];
            Consider(DistanceToSegment(world, lens.Start, lens.End), SceneItemKind.Lens, index);
        }

        var hitItem = SceneGeometry.Enumerate(_scene)
            .FirstOrDefault(item => item.Kind == itemKind && item.Index == itemIndex);
        if (hitItem.Id != Guid.Empty && FindGroupContaining(hitItem.Id) is { } hitGroup)
        {
            UpdateScene(RemoveItems(_scene, hitGroup.MemberIds.ToHashSet()) with
            {
                Groups = _scene.ElementGroups.Where(group => group.Id != hitGroup.Id).ToArray()
            });
            ClearSelection();
            PreviewRequested?.Invoke(this, EventArgs.Empty);
            SceneCommitted?.Invoke(this, EventArgs.Empty);
            InteractionStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        switch (itemKind)
        {
            case SceneItemKind.LightSource:
                UpdateScene(_scene with { LightSources = RemoveAt(_scene.LightSources, itemIndex) });
                break;
            case SceneItemKind.Mirror:
                UpdateScene(_scene with { Mirrors = RemoveAt(_scene.Mirrors, itemIndex) });
                break;
            case SceneItemKind.ConcaveSphericalMirror:
                UpdateScene(_scene with
                {
                    ConcaveSphericalMirrors = RemoveAt(_scene.ConcaveSphericalMirrorElements, itemIndex)
                });
                break;
            case SceneItemKind.ConvexSphericalMirror:
                UpdateScene(_scene with
                {
                    ConvexSphericalMirrors = RemoveAt(_scene.ConvexSphericalMirrorElements, itemIndex)
                });
                break;
            case SceneItemKind.BeamSplitter:
                UpdateScene(_scene with
                {
                    BeamSplitters = RemoveAt(_scene.BeamSplitterElements, itemIndex)
                });
                break;
            case SceneItemKind.Screen:
                UpdateScene(_scene with { Screens = RemoveAt(_scene.ScreenElements, itemIndex) });
                break;
            case SceneItemKind.Aperture:
                UpdateScene(_scene with { Apertures = RemoveAt(_scene.ApertureElements, itemIndex) });
                break;
            case SceneItemKind.ReflectionGrating:
                UpdateScene(_scene with
                {
                    ReflectionGratings = RemoveAt(_scene.ReflectionGratingElements, itemIndex)
                });
                break;
            case SceneItemKind.ConcaveGrating:
                UpdateScene(_scene with
                {
                    ConcaveGratings = RemoveAt(_scene.ConcaveGratingElements, itemIndex)
                });
                break;
            case SceneItemKind.Lens:
                UpdateScene(_scene with { Lenses = RemoveAt(_scene.LensElements, itemIndex) });
                break;
            default:
                return;
        }

        PreviewRequested?.Invoke(this, EventArgs.Empty);
        SceneCommitted?.Invoke(this, EventArgs.Empty);
        InteractionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static OpticalScene RemoveItems(OpticalScene scene, IReadOnlySet<Guid> ids) => scene with
    {
        LightSources = scene.LightSources.Where(item => !ids.Contains(item.Id)).ToArray(),
        Mirrors = scene.Mirrors.Where(item => !ids.Contains(item.Id)).ToArray(),
        ConcaveSphericalMirrors = scene.ConcaveSphericalMirrorElements.Where(item => !ids.Contains(item.Id)).ToArray(),
        ConvexSphericalMirrors = scene.ConvexSphericalMirrorElements.Where(item => !ids.Contains(item.Id)).ToArray(),
        BeamSplitters = scene.BeamSplitterElements.Where(item => !ids.Contains(item.Id)).ToArray(),
        Screens = scene.ScreenElements.Where(item => !ids.Contains(item.Id)).ToArray(),
        Apertures = scene.ApertureElements.Where(item => !ids.Contains(item.Id)).ToArray(),
        ReflectionGratings = scene.ReflectionGratingElements.Where(item => !ids.Contains(item.Id)).ToArray(),
        ConcaveGratings = scene.ConcaveGratingElements.Where(item => !ids.Contains(item.Id)).ToArray(),
        Lenses = scene.LensElements.Where(item => !ids.Contains(item.Id)).ToArray()
    };

    private static T[] RemoveAt<T>(T[] items, int index) =>
        [.. items[..index], .. items[(index + 1)..]];
}
