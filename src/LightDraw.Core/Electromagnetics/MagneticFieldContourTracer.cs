using LightDraw.Core.Geometry;

namespace LightDraw.Core.Electromagnetics;

/// <summary>
/// Extracts in-plane field lines of perpendicular infinite wires as contours of A_z.
/// Equal increments of A_z represent equal magnetic flux, so contour density naturally
/// follows field strength without depending on arbitrary streamline seeds.
/// </summary>
internal sealed class MagneticFieldContourTracer
{
    public List<MagneticFieldLine> Trace(
        VerticalInfiniteCurrentConductor[] conductors,
        Vector2D sceneCenter,
        double maximumDistance,
        int density,
        MagnetostaticSimulationOptions options,
        Func<Vector2D, Vector2D> fieldAt)
    {
        if (conductors.Length == 0) return [];

        // A square grid keeps all four sides equivalent. An even number of cells also places
        // sceneCenter on a grid vertex, preserving reflection symmetry about the scene axes.
        var cellCount = Math.Clamp(density * 16, 128, 384);
        if ((cellCount & 1) != 0) cellCount++;
        var gridSize = cellCount + 1;
        var minimum = sceneCenter - new Vector2D(maximumDistance, maximumDistance);
        var spacing = 2 * maximumDistance / cellCount;
        var values = new double[gridSize, gridSize];

        for (var yIndex = 0; yIndex < gridSize; yIndex++)
        {
            var y = minimum.Y + yIndex * spacing;
            for (var xIndex = 0; xIndex < gridSize; xIndex++)
            {
                var point = new Vector2D(minimum.X + xIndex * spacing, y);
                var value = MagneticVectorPotentialAt(point, conductors, options.CoreRadius);
                values[xIndex, yIndex] = value;
            }
        }

        // Bound the displayed flux range by the outer domain and by a source-centred ring at
        // FirstFieldLineRadius. This retains the useful near-wire contours without allowing the
        // logarithmic singularity of an ideal wire to consume most contour levels.
        var rangeSamples = new List<double>(gridSize * 4 + conductors.Length * 64);
        for (var index = 0; index < gridSize; index++)
        {
            rangeSamples.Add(values[index, 0]);
            rangeSamples.Add(values[index, cellCount]);
            rangeSamples.Add(values[0, index]);
            rangeSamples.Add(values[cellCount, index]);
        }
        foreach (var conductor in conductors)
        {
            for (var angleIndex = 0; angleIndex < 64; angleIndex++)
            {
                var point = conductor.Position + Vector2D.FromAngle(2 * Math.PI * angleIndex / 64) *
                    options.FirstFieldLineRadius;
                rangeSamples.Add(MagneticVectorPotentialAt(point, conductors, options.CoreRadius));
            }
        }
        var lower = rangeSamples.Min();
        var upper = rangeSamples.Max();
        if (!double.IsFinite(lower) || !double.IsFinite(upper) || upper - lower <= 1e-12)
            return [];

        var baseCount = Math.Clamp(density / 2, 3, 24);
        var levelCount = Math.Clamp(baseCount * conductors.Length, 4, 96);
        var lines = new List<MagneticFieldLine>(levelCount * 2);
        for (var levelIndex = 1; levelIndex <= levelCount; levelIndex++)
        {
            var level = lower + (upper - lower) * levelIndex / (levelCount + 1);
            var segments = ExtractSegments(values, minimum, spacing, cellCount, level);
            foreach (var points in StitchSegments(segments, spacing))
            {
                if (points.Count < 3) continue;
                var isClosed = (points[0] - points[^1]).Length <= spacing * 1e-4;
                OrientAlongField(points, fieldAt);
                lines.Add(new MagneticFieldLine(points, isClosed));
            }
        }
        return lines;
    }

    private static double MagneticVectorPotentialAt(
        Vector2D position,
        IReadOnlyList<VerticalInfiniteCurrentConductor> conductors,
        double coreRadius)
    {
        var coreRadiusSquared = Math.Max(coreRadius * coreRadius, 0.01);
        var potential = 0d;
        foreach (var conductor in conductors)
        {
            var distanceSquared = Math.Max(
                (position - conductor.Position).LengthSquared,
                coreRadiusSquared);
            // The common mu0/(4*pi) factor and an additive reference constant do not change
            // contours. -I*ln(r) gives B=(dA_z/dy, -dA_z/dx), matching the right-hand rule.
            potential -= 0.5 * conductor.CurrentAmperes * Math.Log(distanceSquared);
        }
        return potential;
    }

    private static List<ContourSegment> ExtractSegments(
        double[,] values,
        Vector2D minimum,
        double spacing,
        int cellCount,
        double level)
    {
        var segments = new List<ContourSegment>();
        for (var y = 0; y < cellCount; y++)
        {
            for (var x = 0; x < cellCount; x++)
            {
                var v0 = values[x, y];
                var v1 = values[x + 1, y];
                var v2 = values[x + 1, y + 1];
                var v3 = values[x, y + 1];
                var mask = (v0 >= level ? 1 : 0) |
                           (v1 >= level ? 2 : 0) |
                           (v2 >= level ? 4 : 0) |
                           (v3 >= level ? 8 : 0);
                if (mask is 0 or 15) continue;

                var p0 = minimum + new Vector2D(x * spacing, y * spacing);
                var p1 = p0 + new Vector2D(spacing, 0);
                var p2 = p0 + new Vector2D(spacing, spacing);
                var p3 = p0 + new Vector2D(0, spacing);
                Vector2D Edge(int edge) => edge switch
                {
                    0 => Interpolate(p0, p1, v0, v1, level),
                    1 => Interpolate(p1, p2, v1, v2, level),
                    2 => Interpolate(p2, p3, v2, v3, level),
                    _ => Interpolate(p3, p0, v3, v0, level)
                };
                void Add(int firstEdge, int secondEdge) =>
                    segments.Add(new ContourSegment(Edge(firstEdge), Edge(secondEdge)));

                switch (mask)
                {
                    case 1: Add(3, 0); break;
                    case 2: Add(0, 1); break;
                    case 3: Add(3, 1); break;
                    case 4: Add(1, 2); break;
                    case 5:
                        if (AsymptoticDeterminant(v0, v1, v2, v3, level) >= 0)
                        { Add(0, 1); Add(2, 3); }
                        else
                        { Add(3, 0); Add(1, 2); }
                        break;
                    case 6: Add(0, 2); break;
                    case 7: Add(2, 3); break;
                    case 8: Add(2, 3); break;
                    case 9: Add(0, 2); break;
                    case 10:
                        if (AsymptoticDeterminant(v0, v1, v2, v3, level) <= 0)
                        { Add(3, 0); Add(1, 2); }
                        else
                        { Add(0, 1); Add(2, 3); }
                        break;
                    case 11: Add(1, 2); break;
                    case 12: Add(1, 3); break;
                    case 13: Add(0, 1); break;
                    case 14: Add(3, 0); break;
                }
            }
        }
        return segments;
    }

    private static double AsymptoticDeterminant(
        double v0,
        double v1,
        double v2,
        double v3,
        double level) =>
        (v0 - level) * (v2 - level) - (v1 - level) * (v3 - level);

    private static List<List<Vector2D>> StitchSegments(
        IReadOnlyList<ContourSegment> segments,
        double spacing)
    {
        var tolerance = Math.Max(spacing * 1e-6, 1e-9);
        var nodes = new Dictionary<PointKey, ContourNode>();
        ContourNode Node(Vector2D point)
        {
            var key = new PointKey(
                (long)Math.Round(point.X / tolerance),
                (long)Math.Round(point.Y / tolerance));
            if (nodes.TryGetValue(key, out var existing)) return existing;
            var created = new ContourNode(point);
            nodes.Add(key, created);
            return created;
        }

        foreach (var segment in segments)
        {
            var first = Node(segment.First);
            var second = Node(segment.Second);
            if (ReferenceEquals(first, second)) continue;
            first.Neighbours.Add(second);
            second.Neighbours.Add(first);
        }

        var polylines = new List<List<Vector2D>>();
        foreach (var start in nodes.Values.Where(node => node.Neighbours.Count == 1))
            if (!start.Visited) polylines.Add(Walk(start));
        foreach (var start in nodes.Values)
            if (!start.Visited) polylines.Add(Walk(start));
        return polylines;

        static List<Vector2D> Walk(ContourNode start)
        {
            var points = new List<Vector2D>();
            ContourNode? previous = null;
            var current = start;
            while (true)
            {
                points.Add(current.Position);
                current.Visited = true;
                var next = current.Neighbours.FirstOrDefault(candidate =>
                    !ReferenceEquals(candidate, previous) && !candidate.Visited);
                if (next is null)
                {
                    if (current.Neighbours.Contains(start) && !ReferenceEquals(current, start))
                        points.Add(start.Position);
                    break;
                }
                previous = current;
                current = next;
            }
            return points;
        }
    }

    private static void OrientAlongField(List<Vector2D> points, Func<Vector2D, Vector2D> fieldAt)
    {
        var middle = Math.Clamp(points.Count / 2, 1, points.Count - 2);
        var tangent = points[middle + 1] - points[middle - 1];
        var field = fieldAt(points[middle]);
        if (tangent.Dot(field) < 0) points.Reverse();
    }

    private static Vector2D Interpolate(
        Vector2D first,
        Vector2D second,
        double firstValue,
        double secondValue,
        double level)
    {
        var difference = secondValue - firstValue;
        var fraction = Math.Abs(difference) <= 1e-30 ? 0.5 : (level - firstValue) / difference;
        return first + (second - first) * Math.Clamp(fraction, 0, 1);
    }

    private readonly record struct ContourSegment(Vector2D First, Vector2D Second);
    private readonly record struct PointKey(long X, long Y);

    private sealed class ContourNode(Vector2D position)
    {
        public Vector2D Position { get; } = position;
        public List<ContourNode> Neighbours { get; } = [];
        public bool Visited { get; set; }
    }
}
