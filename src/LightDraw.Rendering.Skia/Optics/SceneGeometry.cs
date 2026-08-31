using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;

namespace LightDraw.Rendering.Skia.Optics;

internal readonly record struct SceneItemRef(SceneItemKind Kind, int Index, Guid Id);

internal readonly record struct WorldBounds(double Left, double Top, double Right, double Bottom)
{
    public static WorldBounds FromPoint(Vector2D point) => new(point.X, point.Y, point.X, point.Y);

    public bool Contains(WorldBounds other) =>
        other.Left >= Left && other.Right <= Right && other.Top >= Top && other.Bottom <= Bottom;

    public WorldBounds Include(WorldBounds other) => new(
        Math.Min(Left, other.Left), Math.Min(Top, other.Top),
        Math.Max(Right, other.Right), Math.Max(Bottom, other.Bottom));

    public static WorldBounds FromCorners(Vector2D a, Vector2D b) => new(
        Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
}

internal static class SceneGeometry
{
    public static IEnumerable<SceneItemRef> Enumerate(OpticalScene scene)
    {
        for (var i = 0; i < scene.LightSources.Length; i++)
            yield return new SceneItemRef(SceneItemKind.LightSource, i, scene.LightSources[i].Id);
        for (var i = 0; i < scene.Mirrors.Length; i++)
            yield return new SceneItemRef(SceneItemKind.Mirror, i, scene.Mirrors[i].Id);
        for (var i = 0; i < scene.ConcaveSphericalMirrorElements.Length; i++)
            yield return new SceneItemRef(SceneItemKind.ConcaveSphericalMirror, i,
                scene.ConcaveSphericalMirrorElements[i].Id);
        for (var i = 0; i < scene.ConvexSphericalMirrorElements.Length; i++)
            yield return new SceneItemRef(SceneItemKind.ConvexSphericalMirror, i,
                scene.ConvexSphericalMirrorElements[i].Id);
        for (var i = 0; i < scene.BeamSplitterElements.Length; i++)
            yield return new SceneItemRef(SceneItemKind.BeamSplitter, i, scene.BeamSplitterElements[i].Id);
        for (var i = 0; i < scene.ScreenElements.Length; i++)
            yield return new SceneItemRef(SceneItemKind.Screen, i, scene.ScreenElements[i].Id);
        for (var i = 0; i < scene.ApertureElements.Length; i++)
            yield return new SceneItemRef(SceneItemKind.Aperture, i, scene.ApertureElements[i].Id);
        for (var i = 0; i < scene.ReflectionGratingElements.Length; i++)
            yield return new SceneItemRef(SceneItemKind.ReflectionGrating, i,
                scene.ReflectionGratingElements[i].Id);
        for (var i = 0; i < scene.LensElements.Length; i++)
            yield return new SceneItemRef(SceneItemKind.Lens, i, scene.LensElements[i].Id);
    }

    public static SceneItemRef? Find(OpticalScene scene, Guid id) =>
        Enumerate(scene).FirstOrDefault(item => item.Id == id) is { Id: var found } item && found != Guid.Empty
            ? item
            : null;

    public static Vector2D Origin(OpticalScene scene, SceneItemRef item) => item.Kind switch
    {
        SceneItemKind.LightSource => scene.LightSources[item.Index].Kind == LightSourceKind.ParallelLine &&
                                     scene.LightSources[item.Index].End is { } sourceEnd
            ? (scene.LightSources[item.Index].Position + sourceEnd) / 2
            : scene.LightSources[item.Index].Position,
        SceneItemKind.Mirror => Mid(scene.Mirrors[item.Index].Start, scene.Mirrors[item.Index].End),
        SceneItemKind.ConcaveSphericalMirror => scene.ConcaveSphericalMirrorElements[item.Index].Vertex,
        SceneItemKind.ConvexSphericalMirror => scene.ConvexSphericalMirrorElements[item.Index].Vertex,
        SceneItemKind.BeamSplitter => Mid(scene.BeamSplitterElements[item.Index].Start,
            scene.BeamSplitterElements[item.Index].End),
        SceneItemKind.Screen => Mid(scene.ScreenElements[item.Index].Start, scene.ScreenElements[item.Index].End),
        SceneItemKind.Aperture => Mid(scene.ApertureElements[item.Index].Start,
            scene.ApertureElements[item.Index].End),
        SceneItemKind.ReflectionGrating => Mid(scene.ReflectionGratingElements[item.Index].Start,
            scene.ReflectionGratingElements[item.Index].End),
        SceneItemKind.Lens => Mid(scene.LensElements[item.Index].Start, scene.LensElements[item.Index].End),
        _ => Vector2D.Zero
    };

    public static double AngleDegrees(OpticalScene scene, SceneItemRef item) => item.Kind switch
    {
        SceneItemKind.LightSource => scene.LightSources[item.Index].Kind == LightSourceKind.Point
            ? NormalizeDegrees(scene.LightSources[item.Index].DirectionDegrees)
            : SegmentAngle(scene.LightSources[item.Index].Position, scene.LightSources[item.Index].End!.Value),
        SceneItemKind.Mirror => SegmentAngle(scene.Mirrors[item.Index].Start, scene.Mirrors[item.Index].End),
        SceneItemKind.ConcaveSphericalMirror => SegmentAngle(
            scene.ConcaveSphericalMirrorElements[item.Index].Vertex,
            scene.ConcaveSphericalMirrorElements[item.Index].CenterOfCurvature),
        SceneItemKind.ConvexSphericalMirror => SegmentAngle(
            scene.ConvexSphericalMirrorElements[item.Index].Vertex,
            scene.ConvexSphericalMirrorElements[item.Index].CenterOfCurvature),
        SceneItemKind.BeamSplitter => SegmentAngle(scene.BeamSplitterElements[item.Index].Start,
            scene.BeamSplitterElements[item.Index].End),
        SceneItemKind.Screen => SegmentAngle(scene.ScreenElements[item.Index].Start,
            scene.ScreenElements[item.Index].End),
        SceneItemKind.Aperture => SegmentAngle(scene.ApertureElements[item.Index].Start,
            scene.ApertureElements[item.Index].End),
        SceneItemKind.ReflectionGrating => SegmentAngle(scene.ReflectionGratingElements[item.Index].Start,
            scene.ReflectionGratingElements[item.Index].End),
        SceneItemKind.Lens => SegmentAngle(scene.LensElements[item.Index].Start,
            scene.LensElements[item.Index].End),
        _ => 0
    };

    public static WorldBounds Bounds(OpticalScene scene, SceneItemRef item)
    {
        return item.Kind switch
        {
            SceneItemKind.LightSource => SourceBounds(scene.LightSources[item.Index]),
            SceneItemKind.Mirror => SegmentBounds(scene.Mirrors[item.Index].Start, scene.Mirrors[item.Index].End),
            SceneItemKind.ConcaveSphericalMirror => ArcBounds(
                scene.ConcaveSphericalMirrorElements[item.Index].Vertex,
                scene.ConcaveSphericalMirrorElements[item.Index].CenterOfCurvature,
                scene.ConcaveSphericalMirrorElements[item.Index].ArcAngleDegrees),
            SceneItemKind.ConvexSphericalMirror => ArcBounds(
                scene.ConvexSphericalMirrorElements[item.Index].Vertex,
                scene.ConvexSphericalMirrorElements[item.Index].CenterOfCurvature,
                scene.ConvexSphericalMirrorElements[item.Index].ArcAngleDegrees),
            SceneItemKind.BeamSplitter => SegmentBounds(scene.BeamSplitterElements[item.Index].Start,
                scene.BeamSplitterElements[item.Index].End),
            SceneItemKind.Screen => SegmentBounds(scene.ScreenElements[item.Index].Start,
                scene.ScreenElements[item.Index].End),
            SceneItemKind.Aperture => SegmentBounds(scene.ApertureElements[item.Index].Start,
                scene.ApertureElements[item.Index].End),
            SceneItemKind.ReflectionGrating => SegmentBounds(scene.ReflectionGratingElements[item.Index].Start,
                scene.ReflectionGratingElements[item.Index].End),
            SceneItemKind.Lens => SegmentBounds(scene.LensElements[item.Index].Start,
                scene.LensElements[item.Index].End),
            _ => WorldBounds.FromPoint(Vector2D.Zero)
        };
    }

    public static WorldBounds Bounds(OpticalScene scene, IEnumerable<Guid> ids)
    {
        WorldBounds? result = null;
        foreach (var id in ids)
        {
            if (Find(scene, id) is not { } item) continue;
            result = result is null ? Bounds(scene, item) : result.Value.Include(Bounds(scene, item));
        }
        return result ?? WorldBounds.FromPoint(Vector2D.Zero);
    }

    public static OpticalScene Translate(OpticalScene scene, IReadOnlySet<Guid> ids, Vector2D delta) => scene with
    {
        LightSources = scene.LightSources.Select(item => ids.Contains(item.Id) ? item with
        {
            Position = item.Position + delta,
            End = item.End is { } end ? end + delta : null
        } : item).ToArray(),
        Mirrors = scene.Mirrors.Select(item => ids.Contains(item.Id)
            ? item with { Start = item.Start + delta, End = item.End + delta } : item).ToArray(),
        ConcaveSphericalMirrors = scene.ConcaveSphericalMirrorElements.Select(item => ids.Contains(item.Id)
            ? item with { Vertex = item.Vertex + delta, CenterOfCurvature = item.CenterOfCurvature + delta }
            : item).ToArray(),
        ConvexSphericalMirrors = scene.ConvexSphericalMirrorElements.Select(item => ids.Contains(item.Id)
            ? item with { Vertex = item.Vertex + delta, CenterOfCurvature = item.CenterOfCurvature + delta }
            : item).ToArray(),
        BeamSplitters = scene.BeamSplitterElements.Select(item => ids.Contains(item.Id)
            ? item with { Start = item.Start + delta, End = item.End + delta } : item).ToArray(),
        Screens = scene.ScreenElements.Select(item => ids.Contains(item.Id)
            ? item with { Start = item.Start + delta, End = item.End + delta } : item).ToArray(),
        Apertures = scene.ApertureElements.Select(item => ids.Contains(item.Id)
            ? item with { Start = item.Start + delta, End = item.End + delta } : item).ToArray(),
        ReflectionGratings = scene.ReflectionGratingElements.Select(item => ids.Contains(item.Id)
            ? item with { Start = item.Start + delta, End = item.End + delta } : item).ToArray(),
        Lenses = scene.LensElements.Select(item => ids.Contains(item.Id)
            ? item with { Start = item.Start + delta, End = item.End + delta } : item).ToArray()
    };

    public static OpticalScene Rotate(OpticalScene scene, IReadOnlySet<Guid> ids, Vector2D pivot,
        double radians) => scene with
    {
        LightSources = scene.LightSources.Select(item => ids.Contains(item.Id) ? item with
        {
            Position = RotatePoint(item.Position, pivot, radians),
            End = item.End is { } end ? RotatePoint(end, pivot, radians) : null,
            DirectionDegrees = NormalizeDegrees(item.DirectionDegrees + radians * 180 / Math.PI)
        } : item).ToArray(),
        Mirrors = scene.Mirrors.Select(item => ids.Contains(item.Id) ? item with
        {
            Start = RotatePoint(item.Start, pivot, radians), End = RotatePoint(item.End, pivot, radians)
        } : item).ToArray(),
        ConcaveSphericalMirrors = scene.ConcaveSphericalMirrorElements.Select(item => ids.Contains(item.Id)
            ? item with
            {
                Vertex = RotatePoint(item.Vertex, pivot, radians),
                CenterOfCurvature = RotatePoint(item.CenterOfCurvature, pivot, radians)
            } : item).ToArray(),
        ConvexSphericalMirrors = scene.ConvexSphericalMirrorElements.Select(item => ids.Contains(item.Id)
            ? item with
            {
                Vertex = RotatePoint(item.Vertex, pivot, radians),
                CenterOfCurvature = RotatePoint(item.CenterOfCurvature, pivot, radians)
            } : item).ToArray(),
        BeamSplitters = RotateSegments(scene.BeamSplitterElements, ids, pivot, radians,
            item => item.Id, item => item.Start, item => item.End,
            (item, start, end) => item with { Start = start, End = end }),
        Screens = RotateSegments(scene.ScreenElements, ids, pivot, radians,
            item => item.Id, item => item.Start, item => item.End,
            (item, start, end) => item with { Start = start, End = end }),
        Apertures = RotateSegments(scene.ApertureElements, ids, pivot, radians,
            item => item.Id, item => item.Start, item => item.End,
            (item, start, end) => item with { Start = start, End = end }),
        ReflectionGratings = RotateSegments(scene.ReflectionGratingElements, ids, pivot, radians,
            item => item.Id, item => item.Start, item => item.End,
            (item, start, end) => item with { Start = start, End = end }),
        Lenses = RotateSegments(scene.LensElements, ids, pivot, radians,
            item => item.Id, item => item.Start, item => item.End,
            (item, start, end) => item with { Start = start, End = end })
    };

    private static T[] RotateSegments<T>(IEnumerable<T> items, IReadOnlySet<Guid> ids, Vector2D pivot,
        double radians, Func<T, Guid> getId, Func<T, Vector2D> getStart, Func<T, Vector2D> getEnd,
        Func<T, Vector2D, Vector2D, T> update) where T : notnull
    {
        return items.Select(item =>
        {
            if (!ids.Contains(getId(item))) return item;
            return update(item, RotatePoint(getStart(item), pivot, radians),
                RotatePoint(getEnd(item), pivot, radians));
        }).ToArray();
    }

    private static WorldBounds SourceBounds(LightSource source) => source.End is { } end
        ? SegmentBounds(source.Position, end)
        : WorldBounds.FromPoint(source.Position);

    private static WorldBounds SegmentBounds(Vector2D start, Vector2D end) =>
        WorldBounds.FromCorners(start, end);

    private static WorldBounds ArcBounds(Vector2D vertex, Vector2D center, double sweepDegrees)
    {
        var radius = (vertex - center).Length;
        if (radius <= 1e-12) return WorldBounds.FromPoint(vertex);
        var middle = Math.Atan2(vertex.Y - center.Y, vertex.X - center.X);
        var halfSweep = Math.Abs(sweepDegrees) * Math.PI / 360;
        var result = WorldBounds.FromPoint(vertex);
        const int samples = 32;
        for (var i = 0; i <= samples; i++)
        {
            var angle = middle - halfSweep + 2 * halfSweep * i / samples;
            result = result.Include(WorldBounds.FromPoint(center + Vector2D.FromAngle(angle) * radius));
        }
        return result;
    }

    private static Vector2D RotatePoint(Vector2D point, Vector2D pivot, double radians)
    {
        var offset = point - pivot;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return pivot + new Vector2D(offset.X * cosine - offset.Y * sine,
            offset.X * sine + offset.Y * cosine);
    }

    private static Vector2D Mid(Vector2D start, Vector2D end) => (start + end) / 2;
    private static double SegmentAngle(Vector2D start, Vector2D end) =>
        NormalizeDegrees(Math.Atan2(end.Y - start.Y, end.X - start.X) * 180 / Math.PI);
    private static double NormalizeDegrees(double degrees)
    {
        var result = degrees % 360;
        return result < 0 ? result + 360 : result;
    }
}
