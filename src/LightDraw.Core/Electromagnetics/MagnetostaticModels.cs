using LightDraw.Core.Geometry;

namespace LightDraw.Core.Electromagnetics;

/// <summary>
/// A finite constant-current conductor. Positive current flows from <see cref="Start"/> to
/// <see cref="End"/>; a negative value reverses that direction.
/// </summary>
public sealed record PlanarIdealConstantCurrentConductor(
    Vector2D Start,
    Vector2D End,
    double CurrentAmperes = 1,
    string? Name = null);

/// <summary>An ideal infinitely long conductor perpendicular to the drawing plane.</summary>
public sealed record VerticalInfiniteCurrentConductor(Vector2D Position, double CurrentAmperes = 1,
    string? Name = null);

/// <summary>An ideal circular current loop lying in the drawing plane.</summary>
public sealed record PlanarCircularCurrentLoop(
    Vector2D Center,
    double Radius = 80,
    double CurrentAmperes = 1,
    string? Name = null);

/// <summary>
/// An ideal circular current loop in a plane perpendicular to the drawing plane. Angle identifies
/// the direction of the loop plane's intersection with the drawing plane.
/// </summary>
public sealed record VerticalCircularCurrentLoop(
    Vector2D Center,
    double Radius = 80,
    double AngleDegrees = 0,
    double CurrentAmperes = 1,
    string? Name = null);

public sealed record MagnetostaticScene(
    string Name,
    PlanarIdealConstantCurrentConductor[] Conductors,
    VerticalInfiniteCurrentConductor[]? VerticalConductors = null,
    PlanarCircularCurrentLoop[]? PlanarLoops = null,
    VerticalCircularCurrentLoop[]? VerticalLoops = null)
{
    public VerticalInfiniteCurrentConductor[] VerticalConductorElements => VerticalConductors ?? [];
    public PlanarCircularCurrentLoop[] PlanarLoopElements => PlanarLoops ?? [];
    public VerticalCircularCurrentLoop[] VerticalLoopElements => VerticalLoops ?? [];

    public static MagnetostaticScene CreateEmpty() => new("空白静磁场", [], [], [], []);
}

/// <summary>
/// Magnetic flux density sampled in the drawing plane. A positive value points out of the plane
/// (·); a negative value points into the plane (×).
/// </summary>
public sealed record MagneticFieldSample(Vector2D Position, double NormalTesla);

public sealed record MagneticFieldLine(IReadOnlyList<Vector2D> Points, bool IsClosed);

public sealed record MagnetostaticSimulationResult(
    IReadOnlyList<MagneticFieldSample> Samples,
    IReadOnlyList<MagneticFieldLine> FieldLines,
    TimeSpan Elapsed);

public sealed record MagnetostaticSimulationOptions(
    int MarkerDensity = 16,
    double SamplingPadding = 320,
    double ConductorPanelLength = 18,
    double CoreRadius = 5,
    double FirstFieldLineRadius = 28,
    double FieldLineRadiusStep = 24,
    double TraceStepLength = 6,
    int MaximumTraceSteps = 4000,
    double ClosureDistance = 7,
    int MinimumClosureSteps = 24,
    int LoopIntegrationSegments = 96);
