using LightDraw.Core.Geometry;

namespace LightDraw.Core.Electromagnetics;

/// <summary>A point charge in vacuum. Charge is expressed in nanocoulombs for convenient classroom input.</summary>
public sealed record PointCharge(Vector2D Position, double ChargeNanocoulombs = 1, string? Name = null);

/// <summary>A finite conducting plate held at a prescribed potential relative to infinity.</summary>
public sealed record ChargedPlate(Vector2D Start, Vector2D End, double PotentialVolts = 0, string? Name = null);

public sealed record ElectrostaticScene(string Name, PointCharge[] Charges, ChargedPlate[]? Plates = null)
{
    public ChargedPlate[] PlateElements => Plates ?? [];

    public static ElectrostaticScene CreateEmpty() => new("空白静电场", [], []);
}

public enum ElectricFieldLineTermination
{
    Infinity,
    Charge,
    Conductor,
    Stagnation
}

public sealed record ElectricFieldLine(
    IReadOnlyList<Vector2D> Points,
    Vector2D SourcePosition,
    ElectricFieldLineTermination Termination);

public sealed record ElectrostaticSimulationResult(
    IReadOnlyList<ElectricFieldLine> FieldLines,
    int SeedCount,
    TimeSpan Elapsed)
{
    /// <summary>Explicit charges plus the induced panel charges used to represent conductors.</summary>
    public IReadOnlyList<PointCharge> EffectiveCharges { get; init; } = [];
}

public sealed record ElectrostaticSimulationOptions(
    int LinesPerCharge = 24,
    double SeedRadius = 18,
    double StepLength = 7,
    int MaximumSteps = 700,
    double MaximumDistanceFromOrigin = 2200,
    double ChargeCaptureRadius = 13,
    double PlateCaptureDistance = 5,
    double PlatePanelLength = 25);
