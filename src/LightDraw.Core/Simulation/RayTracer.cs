using System.Diagnostics;
using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;

namespace LightDraw.Core.Simulation;

public sealed class RayTracer
{
    public SimulationResult Trace(OpticalScene scene, SimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        options ??= new SimulationOptions();
        var stopwatch = Stopwatch.StartNew();
        var segments = new List<RaySegment>(scene.LightSources.Length * options.RaysPerSource * 3);
        var reflectedRayCount = 0;
        var refractedRayCount = 0;

        foreach (var source in scene.LightSources)
        {
            foreach (var initialRay in Emit(source, options.RaysPerSource))
            {
                TraceSingleRay(initialRay, scene.Mirrors, scene.LensElements, options, segments,
                    ref reflectedRayCount, ref refractedRayCount);
            }
        }

        stopwatch.Stop();
        return new SimulationResult(segments, scene.LightSources.Length * options.RaysPerSource,
            reflectedRayCount, refractedRayCount, stopwatch.Elapsed);
    }

    private static IEnumerable<Ray2D> Emit(LightSource source, int rayCount)
    {
        var count = Math.Max(1, rayCount);
        if (source.Kind == LightSourceKind.Point)
        {
            count = Math.Max(8, count);
            for (var index = 0; index < count; index++)
            {
                var angle = 2 * Math.PI * index / count;
                yield return new Ray2D(source.Position, Vector2D.FromAngle(angle), source.WavelengthNanometers);
            }

            yield break;
        }

        var direction = Vector2D.FromAngle(DegreesToRadians(source.DirectionDegrees));
        for (var index = 0; index < count; index++)
        {
            var ratio = count == 1 ? 0.5 : (double)index / (count - 1);
            var origin = source.Position;
            if (source.End is { } end)
            {
                origin += (end - source.Position) * ratio;
            }

            yield return new Ray2D(origin, direction, source.WavelengthNanometers);
        }
    }

    private static void TraceSingleRay(
        Ray2D initialRay,
        IReadOnlyList<MirrorSegment> mirrors,
        IReadOnlyList<LensSegment> lenses,
        SimulationOptions options,
        List<RaySegment> output,
        ref int reflectedRayCount,
        ref int refractedRayCount)
    {
        var ray = initialRay with { Direction = initialRay.Direction.Normalized() };
        for (var bounce = 0; bounce <= options.MaximumReflections; bounce++)
        {
            var nearestDistance = double.PositiveInfinity;
            MirrorSegment? nearestMirror = null;
            LensSegment? nearestLens = null;

            foreach (var mirror in mirrors)
            {
                if (TryIntersect(ray, mirror.Start, mirror.End, options.IntersectionEpsilon, out var distance) &&
                    distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestMirror = mirror;
                    nearestLens = null;
                }
            }

            foreach (var lens in lenses)
            {
                if (TryIntersect(ray, lens.Start, lens.End, options.IntersectionEpsilon, out var distance) &&
                    distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestMirror = null;
                    nearestLens = lens;
                }
            }

            if (nearestMirror is null && nearestLens is null)
            {
                output.Add(new RaySegment(ray.Origin,
                    ray.Origin + ray.Direction * options.UnboundedRayLength,
                    ray.WavelengthNanometers, bounce));
                return;
            }

            var hitPoint = ray.Origin + ray.Direction * nearestDistance;
            output.Add(new RaySegment(ray.Origin, hitPoint, ray.WavelengthNanometers, bounce));

            Vector2D nextDirection;
            if (nearestMirror is not null)
            {
                var tangent = (nearestMirror.End - nearestMirror.Start).Normalized();
                nextDirection = ray.Direction.Reflected(tangent.Perpendicular()).Normalized();
                reflectedRayCount++;
            }
            else
            {
                nextDirection = RefractThroughIdealLens(ray, hitPoint, nearestLens!);
                refractedRayCount++;
            }

            ray = new Ray2D(hitPoint + nextDirection * (options.IntersectionEpsilon * 32),
                nextDirection, ray.WavelengthNanometers);
        }
    }

    private static Vector2D RefractThroughIdealLens(Ray2D ray, Vector2D hitPoint, LensSegment lens)
    {
        var midpoint = (lens.Start + lens.End) / 2;
        var tangent = (lens.End - lens.Start).Normalized();
        var opticalAxis = tangent.Perpendicular();
        if (ray.Direction.Dot(opticalAxis) < 0)
        {
            opticalAxis = -opticalAxis;
        }

        var focalLength = Math.Max(1, Math.Abs(lens.FocalLength));
        var forwardComponent = Math.Max(1e-9, ray.Direction.Dot(opticalAxis));
        var incomingSlope = ray.Direction.Dot(tangent) / forwardComponent;
        var height = (hitPoint - midpoint).Dot(tangent);
        var opticalPower = lens.Kind == LensKind.Convex ? -height / focalLength : height / focalLength;
        return (opticalAxis + tangent * (incomingSlope + opticalPower)).Normalized();
    }

    private static bool TryIntersect(Ray2D ray, Vector2D start, Vector2D end, double epsilon,
        out double distance)
    {
        var edge = end - start;
        var denominator = ray.Direction.Cross(edge);
        if (Math.Abs(denominator) <= epsilon)
        {
            distance = 0;
            return false;
        }

        var offset = start - ray.Origin;
        var rayParameter = offset.Cross(edge) / denominator;
        var segmentParameter = offset.Cross(ray.Direction) / denominator;
        var hit = rayParameter > epsilon && segmentParameter >= -epsilon && segmentParameter <= 1 + epsilon;
        distance = hit ? rayParameter : 0;
        return hit;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
