using System.Diagnostics;
using LightDraw.Core.Geometry;

namespace LightDraw.Core.Electromagnetics;

/// <summary>
/// Computes the component of magnetic flux density normal to the drawing plane using the
/// Biot-Savart law. For a conductor lying in that plane, positive values point out of the plane
/// and negative values point into it.
/// </summary>
public sealed class MagnetostaticSimulator
{
    private const double VacuumPermeabilityOverFourPi = 1e-7;

    public MagnetostaticSimulationResult Simulate(
        MagnetostaticScene scene,
        MagnetostaticSimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        options ??= new MagnetostaticSimulationOptions();
        var stopwatch = Stopwatch.StartNew();
        var conductors = scene.Conductors
            .Where(conductor => Math.Abs(conductor.CurrentAmperes) > 1e-12 &&
                                (conductor.End - conductor.Start).Length > 1e-6)
            .ToArray();
        var planarLoops = scene.PlanarLoopElements
            .Where(loop => Math.Abs(loop.CurrentAmperes) > 1e-12 && loop.Radius > 1e-6)
            .ToArray();
        var samples = new List<MagneticFieldSample>();
        var density = Math.Clamp(options.MarkerDensity, 4, 48);
        if (conductors.Length > 0 || planarLoops.Length > 0)
        {
            var minX = Math.Min(
                conductors.Length == 0 ? double.PositiveInfinity : conductors.Min(c => Math.Min(c.Start.X, c.End.X)),
                planarLoops.Length == 0 ? double.PositiveInfinity : planarLoops.Min(loop => loop.Center.X - loop.Radius)) - options.SamplingPadding;
            var maxX = Math.Max(
                conductors.Length == 0 ? double.NegativeInfinity : conductors.Max(c => Math.Max(c.Start.X, c.End.X)),
                planarLoops.Length == 0 ? double.NegativeInfinity : planarLoops.Max(loop => loop.Center.X + loop.Radius)) + options.SamplingPadding;
            var minY = Math.Min(
                conductors.Length == 0 ? double.PositiveInfinity : conductors.Min(c => Math.Min(c.Start.Y, c.End.Y)),
                planarLoops.Length == 0 ? double.PositiveInfinity : planarLoops.Min(loop => loop.Center.Y - loop.Radius)) - options.SamplingPadding;
            var maxY = Math.Max(
                conductors.Length == 0 ? double.NegativeInfinity : conductors.Max(c => Math.Max(c.Start.Y, c.End.Y)),
                planarLoops.Length == 0 ? double.NegativeInfinity : planarLoops.Max(loop => loop.Center.Y + loop.Radius)) + options.SamplingPadding;
            // Start from a fine grid, then retain progressively coarser nested grids as |B| falls.
            // This makes marker density—not just marker size—encode field strength.
            var spacing = Math.Max(10, 300d / density);
            minX = Math.Floor(minX / spacing) * spacing;
            minY = Math.Floor(minY / spacing) * spacing;
            var candidates = new List<(int XIndex, int YIndex, MagneticFieldSample Sample)>();
            var yIndex = 0;
            for (var y = minY; y <= maxY; y += spacing, yIndex++)
            {
                var xIndex = 0;
                for (var x = minX; x <= maxX; x += spacing, xIndex++)
                {
                    var position = new Vector2D(x, y);
                    var normalTesla = MagneticFieldNormalAt(position, conductors, planarLoops, options);
                    if (double.IsFinite(normalTesla) && Math.Abs(normalTesla) > 1e-18)
                        candidates.Add((xIndex, yIndex, new MagneticFieldSample(position, normalTesla)));
                }
            }
            if (candidates.Count > 0)
            {
                var orderedMagnitudes = candidates.Select(candidate => Math.Abs(candidate.Sample.NormalTesla))
                    .OrderBy(value => value).ToArray();
                // A high percentile prevents the large weak-field background from becoming the
                // density reference; the dense band remains visibly attached to the conductor.
                var referenceMagnitude = orderedMagnitudes[(int)Math.Floor((orderedMagnitudes.Length - 1) * .97)];
                foreach (var candidate in candidates)
                {
                    var relative = Math.Abs(candidate.Sample.NormalTesla) /
                                   Math.Max(referenceMagnitude, 1e-30);
                    var stride = relative >= .4 ? 1 : relative >= .15 ? 2 : relative >= .05 ? 4 : 6;
                    if (candidate.XIndex % stride == 0 && candidate.YIndex % stride == 0)
                        samples.Add(candidate.Sample);
                }
            }
        }

        var fieldLines = TraceInPlaneFieldLines(
            scene.VerticalConductorElements, scene.VerticalLoopElements, density, options);

        stopwatch.Stop();
        return new MagnetostaticSimulationResult(samples, fieldLines, stopwatch.Elapsed);
    }

    /// <summary>Returns Bz in teslas. Positive is out of the plane (·), negative is into it (×).</summary>
    public double MagneticFieldNormalAt(
        Vector2D position,
        IReadOnlyList<PlanarIdealConstantCurrentConductor> conductors,
        MagnetostaticSimulationOptions? options = null)
        => MagneticFieldNormalAt(position, conductors, [], options);

    public double MagneticFieldNormalAt(
        Vector2D position,
        IReadOnlyList<PlanarIdealConstantCurrentConductor> conductors,
        IReadOnlyList<PlanarCircularCurrentLoop> loops,
        MagnetostaticSimulationOptions? options = null)
    {
        options ??= new MagnetostaticSimulationOptions();
        var normalTesla = 0d;
        var requestedPanelLength = Math.Clamp(options.ConductorPanelLength, 6, 80);
        var coreRadiusMetres = Math.Max(options.CoreRadius, 0.1) * 1e-3;
        var coreRadiusSquared = coreRadiusMetres * coreRadiusMetres;

        foreach (var conductor in conductors)
        {
            var deltaMillimetres = conductor.End - conductor.Start;
            var length = deltaMillimetres.Length;
            if (length <= 1e-6 || Math.Abs(conductor.CurrentAmperes) <= 1e-12) continue;
            var count = Math.Clamp((int)Math.Ceiling(length / requestedPanelLength), 4, 128);
            var differentialLengthMetres = deltaMillimetres * (1e-3 / count);
            for (var index = 0; index < count; index++)
            {
                var source = conductor.Start + deltaMillimetres * ((index + 0.5) / count);
                var displacementMetres = (position - source) * 1e-3;
                var distanceSquared = Math.Max(displacementMetres.LengthSquared, coreRadiusSquared);
                var crossZ = differentialLengthMetres.Cross(displacementMetres);
                normalTesla += VacuumPermeabilityOverFourPi * conductor.CurrentAmperes * crossZ /
                               (distanceSquared * Math.Sqrt(distanceSquared));
            }
        }

        var segmentCount = Math.Clamp(options.LoopIntegrationSegments, 32, 256);
        var angleStep = 2 * Math.PI / segmentCount;
        foreach (var loop in loops)
        {
            if (loop.Radius <= 1e-6 || Math.Abs(loop.CurrentAmperes) <= 1e-12) continue;
            var radiusMetres = loop.Radius * 1e-3;
            for (var index = 0; index < segmentCount; index++)
            {
                var angle = (index + 0.5) * angleStep;
                var radial = Vector2D.FromAngle(angle);
                var source = loop.Center + radial * loop.Radius;
                var differentialLengthMetres = radial.Perpendicular() * (radiusMetres * angleStep);
                var displacementMetres = (position - source) * 1e-3;
                var distanceSquared = Math.Max(displacementMetres.LengthSquared, coreRadiusSquared);
                var crossZ = differentialLengthMetres.Cross(displacementMetres);
                normalTesla += VacuumPermeabilityOverFourPi * loop.CurrentAmperes * crossZ /
                               (distanceSquared * Math.Sqrt(distanceSquared));
            }
        }
        return normalTesla;
    }

    /// <summary>
    /// Returns the in-plane magnetic flux density produced by all perpendicular infinite wires.
    /// Contributions are vectorially superposed before field-line integration.
    /// </summary>
    public Vector2D MagneticFieldInPlaneAt(
        Vector2D position,
        IReadOnlyList<VerticalInfiniteCurrentConductor> conductors,
        MagnetostaticSimulationOptions? options = null)
        => MagneticFieldInPlaneAt(position, conductors, [], options);

    public Vector2D MagneticFieldInPlaneAt(
        Vector2D position,
        IReadOnlyList<VerticalInfiniteCurrentConductor> conductors,
        IReadOnlyList<VerticalCircularCurrentLoop> loops,
        MagnetostaticSimulationOptions? options = null)
    {
        options ??= new MagnetostaticSimulationOptions();
        const double vacuumPermeabilityOverTwoPi = 2e-7;
        var field = Vector2D.Zero;
        var coreRadiusSquared = Math.Max(options.CoreRadius * options.CoreRadius, 0.01);
        foreach (var conductor in conductors)
        {
            if (Math.Abs(conductor.CurrentAmperes) <= 1e-12) continue;
            var displacementMillimetres = position - conductor.Position;
            var distanceSquaredMillimetres = Math.Max(displacementMillimetres.LengthSquared, coreRadiusSquared);
            field += displacementMillimetres.Perpendicular() *
                     (vacuumPermeabilityOverTwoPi * conductor.CurrentAmperes * 1000 /
                      distanceSquaredMillimetres);
        }

        var segmentCount = Math.Clamp(options.LoopIntegrationSegments, 32, 256);
        var angleStep = 2 * Math.PI / segmentCount;
        var coreRadiusMetres = Math.Max(options.CoreRadius, 0.1) * 1e-3;
        var coreRadiusSquaredMetres = coreRadiusMetres * coreRadiusMetres;
        foreach (var loop in loops)
        {
            if (loop.Radius <= 1e-6 || Math.Abs(loop.CurrentAmperes) <= 1e-12) continue;
            var axis = Vector2D.FromAngle(loop.AngleDegrees * Math.PI / 180);
            var radiusMetres = loop.Radius * 1e-3;
            for (var index = 0; index < segmentCount; index++)
            {
                var angle = (index + 0.5) * angleStep;
                var sine = Math.Sin(angle);
                var cosine = Math.Cos(angle);
                var sourceInPlane = loop.Center + axis * (loop.Radius * cosine);
                var sourceZMetres = radiusMetres * sine;
                var differentialLengthInPlane = axis * (-radiusMetres * sine * angleStep);
                var differentialLengthZ = radiusMetres * cosine * angleStep;
                var displacementInPlane = (position - sourceInPlane) * 1e-3;
                var displacementZ = -sourceZMetres;
                var distanceSquared = Math.Max(
                    displacementInPlane.LengthSquared + displacementZ * displacementZ,
                    coreRadiusSquaredMetres);
                var crossX = differentialLengthInPlane.Y * displacementZ -
                             differentialLengthZ * displacementInPlane.Y;
                var crossY = differentialLengthZ * displacementInPlane.X -
                             differentialLengthInPlane.X * displacementZ;
                var scale = VacuumPermeabilityOverFourPi * loop.CurrentAmperes /
                            (distanceSquared * Math.Sqrt(distanceSquared));
                field += new Vector2D(crossX, crossY) * scale;
            }
        }
        return field;
    }

    private List<MagneticFieldLine> TraceInPlaneFieldLines(
        IReadOnlyList<VerticalInfiniteCurrentConductor> conductors,
        IReadOnlyList<VerticalCircularCurrentLoop> loops,
        int density,
        MagnetostaticSimulationOptions options)
    {
        var lines = new List<MagneticFieldLine>();
        var baseCount = Math.Clamp(density / 2, 3, 24);
        // Double the former outer radius while keeping the near-wire radius small. Equal spacing
        // in log(radius) makes line density proportional to 1/r, matching the field-strength falloff
        // of an ideal infinitely long straight conductor.
        var firstRadius = options.FirstFieldLineRadius;
        var outerRadius = 2 * (firstRadius + (baseCount - 1) * options.FieldLineRadiusStep);
        var activeConductors = conductors.Where(conductor => Math.Abs(conductor.CurrentAmperes) > 1e-12).ToArray();
        var activeLoops = loops.Where(loop => Math.Abs(loop.CurrentAmperes) > 1e-12 && loop.Radius > 1e-6).ToArray();
        if (activeConductors.Length == 0 && activeLoops.Length == 0) return lines;
        var sourceCenters = activeConductors.Select(conductor => conductor.Position)
            .Concat(activeLoops.Select(loop => loop.Center)).ToArray();
        var sceneCenter = sourceCenters.Aggregate(Vector2D.Zero, (sum, center) => sum + center) / sourceCenters.Length;
        var sceneRadius = sourceCenters.Max(center => (center - sceneCenter).Length);
        var maximumDistance = sceneRadius + outerRadius * 4;

        for (var conductorIndex = 0; conductorIndex < activeConductors.Length; conductorIndex++)
        {
            var conductor = activeConductors[conductorIndex];
            // Within the display cap, the number of field lines is proportional to |I| so their
            // overall density communicates magnetic-field strength as well as direction.
            var count = Math.Clamp((int)Math.Round(baseCount * Math.Abs(conductor.CurrentAmperes)), 1, 96);
            for (var lineIndex = 0; lineIndex < count; lineIndex++)
            {
                var ratio = count == 1 ? 0 : (double)lineIndex / (count - 1);
                var radius = firstRadius * Math.Pow(outerRadius / firstRadius, ratio);
                var seedAngle = 2 * Math.PI * ((conductorIndex * 0.61803398875) % 1);
                var seed = FindClearSeed(conductor.Position, radius, seedAngle,
                    activeConductors, activeLoops, options.CoreRadius);
                var line = TraceSuperposedFieldLine(seed, sceneCenter, maximumDistance,
                    activeConductors, activeLoops, options);
                if (line.Points.Count > 2) lines.Add(line);
            }
        }

        for (var loopIndex = 0; loopIndex < activeLoops.Length; loopIndex++)
        {
            var loop = activeLoops[loopIndex];
            var count = Math.Clamp((int)Math.Round(baseCount * Math.Abs(loop.CurrentAmperes)), 4, 96);
            var axis = Vector2D.FromAngle(loop.AngleDegrees * Math.PI / 180);
            var normal = axis.Perpendicular();
            for (var lineIndex = 0; lineIndex < count; lineIndex++)
            {
                // Seeds span the loop interior and exterior. Interior seeds tend to produce closed
                // return paths; exterior seeds may leave the finite display region, matching the
                // requested closed/diverging field-line presentation.
                var fraction = count == 1 ? 0 : (double)lineIndex / (count - 1);
                var signedOffset = (fraction * 2 - 1) * loop.Radius * 0.82;
                var bias = normal * (options.CoreRadius * (1.5 + loopIndex * 0.15));
                var seed = loop.Center + axis * signedOffset + bias;
                var line = TraceSuperposedFieldLine(seed, sceneCenter, maximumDistance,
                    activeConductors, activeLoops, options);
                if (line.Points.Count > 2) lines.Add(line);
            }
        }
        return lines;
    }

    private static Vector2D FindClearSeed(
        Vector2D center,
        double radius,
        double initialAngle,
        IReadOnlyList<VerticalInfiniteCurrentConductor> conductors,
        IReadOnlyList<VerticalCircularCurrentLoop> loops,
        double coreRadius)
    {
        for (var attempt = 0; attempt < 14; attempt++)
        {
            var angle = initialAngle + attempt * Math.PI / 7;
            var candidate = center + Vector2D.FromAngle(angle) * radius;
            if (DistanceToNearestInPlaneSource(candidate, conductors, loops) > coreRadius * 2)
                return candidate;
        }
        return center + Vector2D.FromAngle(initialAngle) * radius;
    }

    private MagneticFieldLine TraceSuperposedFieldLine(
        Vector2D seed,
        Vector2D sceneCenter,
        double maximumDistance,
        VerticalInfiniteCurrentConductor[] conductors,
        VerticalCircularCurrentLoop[] loops,
        MagnetostaticSimulationOptions options)
    {
        var forward = TraceDirection(seed, 1, sceneCenter, maximumDistance, conductors, loops, options);
        if (forward.IsClosed) return new(forward.Points, true);

        var backward = TraceDirection(seed, -1, sceneCenter, maximumDistance, conductors, loops, options);
        if (backward.IsClosed)
        {
            backward.Points.Reverse();
            return new(backward.Points, true);
        }

        // Open field lines need both halves. Reversing the -B trace and appending the +B trace
        // preserves the physical arrow direction along the complete visible polyline.
        backward.Points.Reverse();
        backward.Points.AddRange(forward.Points.Skip(1));
        return new(backward.Points, false);
    }

    private TraceResult TraceDirection(
        Vector2D seed,
        double directionSign,
        Vector2D sceneCenter,
        double maximumDistance,
        VerticalInfiniteCurrentConductor[] conductors,
        VerticalCircularCurrentLoop[] loops,
        MagnetostaticSimulationOptions options)
    {
        var points = new List<Vector2D>(options.MaximumTraceSteps + 2) { seed };
        var current = seed;
        var initialDirection = MagneticFieldInPlaneAt(seed, conductors, loops, options).Normalized() * directionSign;
        if (initialDirection.LengthSquared < 1e-12) return new(points, false);

        for (var stepIndex = 0; stepIndex < options.MaximumTraceSteps; stepIndex++)
        {
            var nearestSourceDistance = DistanceToNearestInPlaneSource(current, conductors, loops);
            var stepLength = Math.Min(options.TraceStepLength, Math.Max(1.2, nearestSourceDistance * 0.08));
            var next = RungeKuttaStep(current, stepLength, directionSign, conductors, loops, options);
            if (!double.IsFinite(next.X) || !double.IsFinite(next.Y) || (next - current).LengthSquared < 1e-12)
                return new(points, false);
            points.Add(next);
            current = next;

            if (stepIndex >= options.MinimumClosureSteps && (current - seed).Length <= options.ClosureDistance)
            {
                var returnDirection = MagneticFieldInPlaneAt(current, conductors, loops, options).Normalized() * directionSign;
                if (returnDirection.Dot(initialDirection) > 0.5)
                {
                    points.Add(seed);
                    return new(points, true);
                }
            }
            if ((current - sceneCenter).Length > maximumDistance) return new(points, false);
            if (DistanceToNearestInPlaneSource(current, conductors, loops) < options.CoreRadius * 0.8)
                return new(points, false);
        }
        return new(points, false);
    }

    private Vector2D RungeKuttaStep(
        Vector2D position,
        double stepLength,
        double directionSign,
        VerticalInfiniteCurrentConductor[] conductors,
        VerticalCircularCurrentLoop[] loops,
        MagnetostaticSimulationOptions options)
    {
        Vector2D UnitField(Vector2D point) =>
            MagneticFieldInPlaneAt(point, conductors, loops, options).Normalized() * directionSign;
        var k1 = UnitField(position);
        var k2 = UnitField(position + k1 * (stepLength / 2));
        var k3 = UnitField(position + k2 * (stepLength / 2));
        var k4 = UnitField(position + k3 * stepLength);
        var delta = (k1 + k2 * 2 + k3 * 2 + k4) / 6;
        return delta.LengthSquared < 1e-12 ? position : position + delta.Normalized() * stepLength;
    }

    private static double DistanceToNearestInPlaneSource(
        Vector2D point,
        IReadOnlyList<VerticalInfiniteCurrentConductor> conductors,
        IReadOnlyList<VerticalCircularCurrentLoop> loops)
    {
        var distance = conductors.Count == 0
            ? double.PositiveInfinity
            : conductors.Min(conductor => (point - conductor.Position).Length);
        foreach (var loop in loops)
        {
            var axis = Vector2D.FromAngle(loop.AngleDegrees * Math.PI / 180);
            distance = Math.Min(distance, (point - (loop.Center + axis * loop.Radius)).Length);
            distance = Math.Min(distance, (point - (loop.Center - axis * loop.Radius)).Length);
        }
        return distance;
    }

    private sealed record TraceResult(List<Vector2D> Points, bool IsClosed);
}
