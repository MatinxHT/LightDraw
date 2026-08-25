using System.Diagnostics;
using LightDraw.Core.Geometry;

namespace LightDraw.Core.Electromagnetics;

/// <summary>
/// Traces vacuum electric-field lines using Coulomb's law and RK4 integration. Finite conducting
/// plates are represented by boundary-element point panels whose charges are solved so that each
/// panel has the requested potential. Coulomb potential tends to zero at infinity, which fixes the
/// otherwise arbitrary potential reference.
/// </summary>
public sealed class ElectrostaticSimulator
{
    private const double CoulombConstant = 8.9875517923e9;
    private const double NanocoulombsToCoulombs = 1e-9;

    public ElectrostaticSimulationResult Simulate(
        ElectrostaticScene scene,
        ElectrostaticSimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        options ??= new ElectrostaticSimulationOptions();
        var stopwatch = Stopwatch.StartNew();
        var lines = new List<ElectricFieldLine>();
        var effectiveCharges = ResolveEffectiveCharges(scene, options);
        var nonZeroCharges = effectiveCharges.Where(charge => Math.Abs(charge.ChargeNanocoulombs) > 1e-10).ToArray();
        if (nonZeroCharges.Length == 0)
        {
            return new ElectrostaticSimulationResult(lines, 0, stopwatch.Elapsed)
            {
                EffectiveCharges = effectiveCharges
            };
        }

        var positiveCharges = nonZeroCharges.Where(charge => charge.ChargeNanocoulombs > 0).ToArray();
        var negativeCharges = nonZeroCharges.Where(charge => charge.ChargeNanocoulombs < 0).ToArray();
        var referenceMagnitude = nonZeroCharges.Average(charge => Math.Abs(charge.ChargeNanocoulombs));
        var seedCount = 0;

        if (positiveCharges.Length > 0)
        {
            seedCount += TraceFromCharges(positiveCharges, 1, false, referenceMagnitude,
                nonZeroCharges, scene.PlateElements, options, lines);
            // Backward tracing from negative charges supplies only the field lines that originate at
            // infinity. Lines reaching a positive charge are discarded because they already exist above.
            seedCount += TraceFromCharges(negativeCharges, -1, true, referenceMagnitude,
                nonZeroCharges, scene.PlateElements, options, lines);
        }
        else
        {
            seedCount += TraceFromCharges(negativeCharges, -1, false, referenceMagnitude,
                nonZeroCharges, scene.PlateElements, options, lines);
        }

        stopwatch.Stop();
        return new ElectrostaticSimulationResult(lines, seedCount, stopwatch.Elapsed)
        {
            EffectiveCharges = effectiveCharges
        };
    }

    private int TraceFromCharges(
        PointCharge[] seedCharges,
        double direction,
        bool keepOnlyInfinity,
        double referenceMagnitude,
        PointCharge[] allCharges,
        ChargedPlate[] plates,
        ElectrostaticSimulationOptions options,
        List<ElectricFieldLine> lines)
    {
        var seedCount = 0;
        foreach (var charge in seedCharges)
        {
            var proportionalCount = options.LinesPerCharge *
                                    Math.Sqrt(Math.Abs(charge.ChargeNanocoulombs) / referenceMagnitude);
            var count = Math.Clamp((int)Math.Round(proportionalCount), 1, options.LinesPerCharge * 4);
            for (var index = 0; index < count; index++)
            {
                var angle = 2 * Math.PI * index / count;
                var seed = charge.Position + Vector2D.FromAngle(angle) * options.SeedRadius;
                var (points, termination) = TraceLine(seed, direction, charge, allCharges, plates, options);
                seedCount++;
                if (points.Count > 1 && termination != ElectricFieldLineTermination.Stagnation &&
                    (!keepOnlyInfinity || termination == ElectricFieldLineTermination.Infinity))
                {
                    lines.Add(new ElectricFieldLine(points, charge.Position, termination));
                }
            }
        }
        return seedCount;
    }

    public Vector2D ElectricFieldAt(Vector2D position, IReadOnlyList<PointCharge> charges)
    {
        var field = Vector2D.Zero;
        foreach (var charge in charges)
        {
            var displacement = position - charge.Position;
            var distanceSquared = Math.Max(displacement.LengthSquared, 1e-12);
            field += displacement.Normalized() *
                     (CoulombConstant * charge.ChargeNanocoulombs * NanocoulombsToCoulombs / distanceSquared);
        }
        return field;
    }

    /// <summary>Returns potential in volts with V(∞) = 0.</summary>
    public double ElectricPotentialAt(Vector2D position, IReadOnlyList<PointCharge> charges)
    {
        var potential = 0d;
        foreach (var charge in charges)
        {
            var distanceMetres = Math.Max((position - charge.Position).Length * 1e-3, 1e-9);
            potential += CoulombConstant * charge.ChargeNanocoulombs * NanocoulombsToCoulombs / distanceMetres;
        }
        return potential;
    }

    private (List<Vector2D> Points, ElectricFieldLineTermination Termination) TraceLine(
        Vector2D seed,
        double direction,
        PointCharge source,
        PointCharge[] charges,
        ChargedPlate[] plates,
        ElectrostaticSimulationOptions options)
    {
        var points = new List<Vector2D>(options.MaximumSteps + 1) { seed };
        var current = seed;
        for (var step = 0; step < options.MaximumSteps; step++)
        {
            var next = RungeKuttaStep(current, direction, charges, options.StepLength);
            if (!double.IsFinite(next.X) || !double.IsFinite(next.Y))
            {
                return (points, ElectricFieldLineTermination.Stagnation);
            }
            if ((next - source.Position).Length > options.MaximumDistanceFromOrigin)
            {
                return (points, ElectricFieldLineTermination.Infinity);
            }

            points.Add(next);
            current = next;
            if (plates.Any(plate => DistanceToSegment(current, plate.Start, plate.End) <= options.PlateCaptureDistance))
            {
                return (points, ElectricFieldLineTermination.Conductor);
            }
            var captured = charges.Any(charge =>
                charge.ChargeNanocoulombs * direction < 0 &&
                (current - charge.Position).Length <= options.ChargeCaptureRadius);
            if (captured)
            {
                return (points, ElectricFieldLineTermination.Charge);
            }

            if (ElectricFieldAt(current, charges).LengthSquared < 1e-20)
            {
                return (points, ElectricFieldLineTermination.Stagnation);
            }
        }
        return (points, ElectricFieldLineTermination.Infinity);
    }

    private Vector2D RungeKuttaStep(Vector2D position, double direction, PointCharge[] charges, double step)
    {
        Vector2D UnitField(Vector2D point) => ElectricFieldAt(point, charges).Normalized() * direction;
        var k1 = UnitField(position);
        var k2 = UnitField(position + k1 * (step / 2));
        var k3 = UnitField(position + k2 * (step / 2));
        var k4 = UnitField(position + k3 * step);
        var delta = (k1 + k2 * 2 + k3 * 2 + k4) / 6;
        return delta.LengthSquared <= 1e-12 ? position : position + delta.Normalized() * step;
    }

    private PointCharge[] ResolveEffectiveCharges(ElectrostaticScene scene, ElectrostaticSimulationOptions options)
    {
        var panels = CreatePanels(scene.PlateElements, options.PlatePanelLength);
        if (panels.Count == 0) return scene.Charges.ToArray();

        var matrix = new double[panels.Count, panels.Count];
        var rightHandSide = new double[panels.Count];
        for (var row = 0; row < panels.Count; row++)
        {
            rightHandSide[row] = panels[row].PotentialVolts - ElectricPotentialAt(panels[row].Position, scene.Charges);
            for (var column = 0; column < panels.Count; column++)
            {
                var distanceMillimetres = row == column
                    ? Math.Max(panels[column].Length * 0.35, 0.5)
                    : Math.Max((panels[row].Position - panels[column].Position).Length, 0.5);
                matrix[row, column] = CoulombConstant * NanocoulombsToCoulombs /
                                      (distanceMillimetres * 1e-3);
            }
        }

        var panelCharges = SolveLinearSystem(matrix, rightHandSide);
        return scene.Charges.Concat(panels.Select((panel, index) =>
            new PointCharge(panel.Position, panelCharges[index]))).ToArray();
    }

    private static List<PlatePanel> CreatePanels(IReadOnlyList<ChargedPlate> plates, double requestedPanelLength)
    {
        var panels = new List<PlatePanel>();
        var panelLength = Math.Clamp(requestedPanelLength, 8, 80);
        foreach (var plate in plates)
        {
            var delta = plate.End - plate.Start;
            var length = delta.Length;
            if (length <= 1e-6) continue;
            var count = Math.Clamp((int)Math.Ceiling(length / panelLength), 4, 32);
            var actualLength = length / count;
            for (var index = 0; index < count; index++)
            {
                var position = plate.Start + delta * ((index + 0.5) / count);
                panels.Add(new PlatePanel(position, actualLength, plate.PotentialVolts));
            }
        }
        return panels;
    }

    private static double[] SolveLinearSystem(double[,] matrix, double[] values)
    {
        var size = values.Length;
        var augmented = new double[size, size + 1];
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++) augmented[row, column] = matrix[row, column];
            augmented[row, size] = values[row];
        }

        for (var pivot = 0; pivot < size; pivot++)
        {
            var bestRow = pivot;
            for (var row = pivot + 1; row < size; row++)
                if (Math.Abs(augmented[row, pivot]) > Math.Abs(augmented[bestRow, pivot])) bestRow = row;
            if (bestRow != pivot)
                for (var column = pivot; column <= size; column++)
                    (augmented[pivot, column], augmented[bestRow, column]) =
                        (augmented[bestRow, column], augmented[pivot, column]);

            var divisor = augmented[pivot, pivot];
            if (Math.Abs(divisor) < 1e-12) continue;
            for (var row = pivot + 1; row < size; row++)
            {
                var factor = augmented[row, pivot] / divisor;
                for (var column = pivot; column <= size; column++)
                    augmented[row, column] -= factor * augmented[pivot, column];
            }
        }

        var result = new double[size];
        for (var row = size - 1; row >= 0; row--)
        {
            var value = augmented[row, size];
            for (var column = row + 1; column < size; column++) value -= augmented[row, column] * result[column];
            result[row] = Math.Abs(augmented[row, row]) < 1e-12 ? 0 : value / augmented[row, row];
        }
        return result;
    }

    private static double DistanceToSegment(Vector2D point, Vector2D start, Vector2D end)
    {
        var delta = end - start;
        if (delta.LengthSquared <= 1e-12) return (point - start).Length;
        var ratio = Math.Clamp((point - start).Dot(delta) / delta.LengthSquared, 0, 1);
        return (point - (start + delta * ratio)).Length;
    }

    private sealed record PlatePanel(Vector2D Position, double Length, double PotentialVolts);
}
