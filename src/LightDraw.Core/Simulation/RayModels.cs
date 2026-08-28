using LightDraw.Core.Geometry;

namespace LightDraw.Core.Simulation;

public enum RaySpectrumState
{
    Monochromatic,
    Composite,
    DispersedComponent
}

public readonly record struct Ray2D(
    Vector2D Origin,
    Vector2D Direction,
    double WavelengthNanometers,
    int DiffractionOrder = 0,
    double Intensity = 1,
    RaySpectrumState SpectrumState = RaySpectrumState.Monochromatic);

public readonly record struct RaySegment(
    Vector2D Start,
    Vector2D End,
    double WavelengthNanometers,
    int BounceIndex,
    int DiffractionOrder = 0,
    double Intensity = 1,
    RaySpectrumState SpectrumState = RaySpectrumState.Monochromatic);

public sealed record SimulationResult(
    IReadOnlyList<RaySegment> Segments,
    int InitialRayCount,
    int ReflectedRayCount,
    int RefractedRayCount,
    int DiffractedRayCount,
    TimeSpan Elapsed);

public sealed record SimulationOptions(
    int RaysPerSource = 160,
    int MaximumReflections = 12,
    double UnboundedRayLength = 2400,
    double IntersectionEpsilon = 1e-7,
    int MaximumDiffractionOrder = 3,
    int MaximumSegments = 100000);
