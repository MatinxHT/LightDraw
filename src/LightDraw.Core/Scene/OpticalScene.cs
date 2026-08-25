using LightDraw.Core.Geometry;
using System.Text.Json.Serialization;

namespace LightDraw.Core.Scene;

public enum LightSourceKind
{
    Point,
    ParallelLine
}

public sealed record LightSource(
    Vector2D Position,
    double DirectionDegrees,
    double SpreadDegrees = 34,
    double WavelengthNanometers = 589,
    LightSourceKind Kind = LightSourceKind.Point,
    Vector2D? End = null);

public sealed record MirrorSegment(Vector2D Start, Vector2D End);

public sealed record BeamSplitterSegment(Vector2D Start, Vector2D End);

public sealed record ScreenSegment(Vector2D Start, Vector2D End);

public sealed record ApertureSegment(Vector2D Start, Vector2D End, double OpeningSize);

public sealed record ReflectionGratingSegment(
    Vector2D Start,
    Vector2D End,
    double GrooveDensityLinesPerMillimeter);

public enum LensKind
{
    Convex,
    Concave
}

public sealed record LensSegment(
    Vector2D Start,
    Vector2D End,
    LensKind Kind,
    double FocalLength);

public sealed record OpticalScene(
    string Name,
    LightSource[] LightSources,
    MirrorSegment[] Mirrors,
    LensSegment[]? Lenses = null,
    ScreenSegment[]? Screens = null,
    ApertureSegment[]? Apertures = null,
    ReflectionGratingSegment[]? ReflectionGratings = null,
    BeamSplitterSegment[]? BeamSplitters = null)
{
    [JsonIgnore]
    public LensSegment[] LensElements => Lenses ?? [];

    [JsonIgnore]
    public ScreenSegment[] ScreenElements => Screens ?? [];

    [JsonIgnore]
    public ApertureSegment[] ApertureElements => Apertures ?? [];

    [JsonIgnore]
    public ReflectionGratingSegment[] ReflectionGratingElements => ReflectionGratings ?? [];

    [JsonIgnore]
    public BeamSplitterSegment[] BeamSplitterElements => BeamSplitters ?? [];

    public static OpticalScene CreateEmpty() => new(
        "空白场景",
        [],
        [],
        [],
        [],
        [],
        [],
        []);

    public static OpticalScene CreateDemo() => new(
        "双镜面反射演示",
        [new LightSource(new Vector2D(-300, 20), -8, 38, 589)],
        [
            new MirrorSegment(new Vector2D(40, -170), new Vector2D(105, 165)),
            new MirrorSegment(new Vector2D(260, -130), new Vector2D(430, 80)),
            new MirrorSegment(new Vector2D(-70, 230), new Vector2D(285, 230))
        ]);
}
