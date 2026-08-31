using LightDraw.Core.Geometry;
using System.Text.Json.Serialization;

namespace LightDraw.Core.Scene;

public enum LightSourceKind
{
    Point,
    ParallelLine
}

public enum LightSpectrumKind
{
    Monochromatic,
    Composite
}

public sealed record LightSource(
    Vector2D Position,
    double DirectionDegrees,
    double SpreadDegrees = 360,
    double WavelengthNanometers = 580,
    LightSourceKind Kind = LightSourceKind.Point,
    Vector2D? End = null,
    LightSpectrumKind Spectrum = LightSpectrumKind.Monochromatic)
{
    public const double MonochromaticWavelengthNanometers = 580;
    public const double CompositeBlueWavelengthNanometers = 450;
    public const double CompositeGreenWavelengthNanometers = 550;
    public const double CompositeRedWavelengthNanometers = 650;
}

public sealed record MirrorSegment(Vector2D Start, Vector2D End);

public sealed record ConcaveSphericalMirror(
    Vector2D Vertex,
    Vector2D CenterOfCurvature,
    double ArcAngleDegrees = 180)
{
    [JsonIgnore]
    public double Radius => (CenterOfCurvature - Vertex).Length;

    [JsonIgnore]
    public double FocalLength => Radius / 2;
}

public sealed record ConvexSphericalMirror(
    Vector2D Vertex,
    Vector2D CenterOfCurvature,
    double ArcAngleDegrees = 180)
{
    [JsonIgnore]
    public double Radius => (CenterOfCurvature - Vertex).Length;

    [JsonIgnore]
    public double FocalLength => Radius / 2;
}

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

public enum LensDispersionMode
{
    None,
    Normal,
    Anomalous
}

public sealed record LensSegment(
    Vector2D Start,
    Vector2D End,
    LensKind Kind,
    double FocalLength,
    LensDispersionMode DispersionMode = LensDispersionMode.None,
    int DispersionLevel = 5);

public sealed record OpticalScene(
    string Name,
    LightSource[] LightSources,
    MirrorSegment[] Mirrors,
    LensSegment[]? Lenses = null,
    ScreenSegment[]? Screens = null,
    ApertureSegment[]? Apertures = null,
    ReflectionGratingSegment[]? ReflectionGratings = null,
    BeamSplitterSegment[]? BeamSplitters = null,
    ConcaveSphericalMirror[]? ConcaveSphericalMirrors = null,
    ConvexSphericalMirror[]? ConvexSphericalMirrors = null)
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

    [JsonIgnore]
    public ConcaveSphericalMirror[] ConcaveSphericalMirrorElements => ConcaveSphericalMirrors ?? [];

    [JsonIgnore]
    public ConvexSphericalMirror[] ConvexSphericalMirrorElements => ConvexSphericalMirrors ?? [];

    public static OpticalScene CreateEmpty() => new(
        "空白场景",
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        []);

    public static OpticalScene CreateDemo() => new(
        "双镜面反射演示",
        [new LightSource(new Vector2D(-300, 20), -8, 38)],
        [
            new MirrorSegment(new Vector2D(40, -170), new Vector2D(105, 165)),
            new MirrorSegment(new Vector2D(260, -130), new Vector2D(430, 80)),
            new MirrorSegment(new Vector2D(-70, 230), new Vector2D(285, 230))
        ]);
}
