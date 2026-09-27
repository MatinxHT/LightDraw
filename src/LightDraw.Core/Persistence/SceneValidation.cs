using LightDraw.Core.Electromagnetics;
using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;

namespace LightDraw.Core.Persistence;

/// <summary>Rejects malformed scene data before it reaches an editor or simulator.</summary>
internal static class SceneValidation
{
    public static void Validate(OpticalScene scene)
    {
        Items(scene.LightSources, "lightSources"); Items(scene.Mirrors, "mirrors");
        var ids = new HashSet<Guid>();
        void Id(Guid id) { if (id != Guid.Empty && !ids.Add(id)) Invalid("duplicate id"); }
        foreach (var item in scene.LightSources)
        {
            Id(item.Id); Point(item.Position); if (item.End is { } end) Segment(item.Position, end);
            Number(item.DirectionDegrees); Number(item.SpreadDegrees); Number(item.WavelengthNanometers);
            if (!Enum.IsDefined(item.Kind) || !Enum.IsDefined(item.Spectrum)) Invalid("light source kind/spectrum");
        }
        foreach (var item in scene.Mirrors) { Id(item.Id); Segment(item.Start, item.End); }
        foreach (var item in Items(scene.ConcaveSphericalMirrorElements, "concaveSphericalMirrors"))
        { Id(item.Id); Segment(item.Vertex, item.CenterOfCurvature); Number(item.ArcAngleDegrees); }
        foreach (var item in Items(scene.ConvexSphericalMirrorElements, "convexSphericalMirrors"))
        { Id(item.Id); Segment(item.Vertex, item.CenterOfCurvature); Number(item.ArcAngleDegrees); }
        foreach (var item in Items(scene.BeamSplitterElements, "beamSplitters")) { Id(item.Id); Segment(item.Start, item.End); }
        foreach (var item in Items(scene.ScreenElements, "screens")) { Id(item.Id); Segment(item.Start, item.End); }
        foreach (var item in Items(scene.ApertureElements, "apertures"))
        { Id(item.Id); Segment(item.Start, item.End); Number(item.OpeningSize); if (item.OpeningSize < 0) Invalid("openingSize"); }
        foreach (var item in Items(scene.ReflectionGratingElements, "reflectionGratings"))
        { Id(item.Id); Segment(item.Start, item.End); Positive(item.GrooveDensityLinesPerMillimeter); }
        foreach (var item in Items(scene.ConcaveGratingElements, "concaveGratings"))
        { Id(item.Id); Segment(item.Vertex, item.CenterOfCurvature); Number(item.ArcAngleDegrees); Positive(item.GrooveDensityLinesPerMillimeter); }
        foreach (var item in Items(scene.LensElements, "lenses"))
        { Id(item.Id); Segment(item.Start, item.End); Positive(Math.Abs(item.FocalLength)); if (!Enum.IsDefined(item.Kind)) Invalid("lens kind"); }
        foreach (var group in Items(scene.ElementGroups, "groups")) Id(group.Id);
    }

    public static void Validate(ElectrostaticScene scene)
    {
        foreach (var item in Items(scene.Charges, "charges")) { Point(item.Position); Number(item.ChargeNanocoulombs); }
        foreach (var item in Items(scene.PlateElements, "plates")) { Segment(item.Start, item.End); Number(item.PotentialVolts); }
    }

    public static void Validate(MagnetostaticScene scene)
    {
        foreach (var item in Items(scene.Conductors, "conductors")) { Segment(item.Start, item.End); Number(item.CurrentAmperes); }
        foreach (var item in Items(scene.VerticalConductorElements, "verticalConductors")) { Point(item.Position); Number(item.CurrentAmperes); }
        foreach (var item in Items(scene.PlanarLoopElements, "planarLoops")) { Point(item.Center); Positive(item.Radius); Number(item.CurrentAmperes); }
        foreach (var item in Items(scene.VerticalLoopElements, "verticalLoops"))
        { Point(item.Center); Positive(item.Radius); Number(item.AngleDegrees); Number(item.CurrentAmperes); }
    }

    private static T[] Items<T>(T[]? items, string field) where T : class
    {
        if (items is null || items.Length > 10000 || items.Any(item => item is null)) Invalid(field);
        return items!;
    }
    private static void Point(Vector2D point) { Number(point.X); Number(point.Y); }
    private static void Segment(Vector2D start, Vector2D end)
    {
        Point(start); Point(end);
        if ((end - start).LengthSquared <= 1e-12) Invalid("zero-length element");
    }
    private static void Positive(double value) { Number(value); if (value <= 0) Invalid("non-positive dimension"); }
    private static void Number(double value)
    {
        if (!double.IsFinite(value) || Math.Abs(value) > 1e9) Invalid("number outside supported range");
    }
    private static void Invalid(string field) => throw new InvalidDataException($"无效的场景数据 / Invalid scene data: {field}.");
}
