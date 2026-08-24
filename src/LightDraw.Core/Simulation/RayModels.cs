using LightDraw.Core.Geometry;

namespace LightDraw.Core.Simulation;

public readonly record struct Ray2D(Vector2D Origin, Vector2D Direction, double WavelengthNanometers);

public readonly record struct RaySegment(
    Vector2D Start,
    Vector2D End,
    double WavelengthNanometers,
    int BounceIndex);

public sealed record SimulationResult(
    IReadOnlyList<RaySegment> Segments,
    int InitialRayCount,
    int ReflectedRayCount,
    int RefractedRayCount,
    TimeSpan Elapsed);

public sealed record SimulationOptions(
    int RaysPerSource = 160,
    int MaximumReflections = 12,
    double UnboundedRayLength = 2400,
    double IntersectionEpsilon = 1e-7);
