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
        var diffractedRayCount = 0;

        foreach (var source in scene.LightSources)
        {
            foreach (var initialRay in Emit(source, options.RaysPerSource))
            {
                TraceSingleRay(initialRay, scene, options, segments,
                    ref reflectedRayCount, ref refractedRayCount, ref diffractedRayCount);
            }
        }

        stopwatch.Stop();
        return new SimulationResult(segments, scene.LightSources.Length * options.RaysPerSource,
            reflectedRayCount, refractedRayCount, diffractedRayCount, stopwatch.Elapsed);
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
        OpticalScene scene,
        SimulationOptions options,
        List<RaySegment> output,
        ref int reflectedRayCount,
        ref int refractedRayCount,
        ref int diffractedRayCount)
    {
        var pending = new Queue<(Ray2D Ray, int InteractionDepth)>();
        pending.Enqueue((initialRay with { Direction = initialRay.Direction.Normalized() }, 0));

        while (pending.Count > 0 && output.Count < options.MaximumSegments)
        {
            var (rayAtStart, interactionDepth) = pending.Dequeue();
            var ray = rayAtStart;
            for (var bounce = interactionDepth;
                 bounce <= options.MaximumReflections && output.Count < options.MaximumSegments;
                 bounce++)
            {
                var hit = FindNearestHit(ray, scene, options.IntersectionEpsilon);
                if (hit is not { } nearestHit)
                {
                    output.Add(new RaySegment(ray.Origin,
                        ray.Origin + ray.Direction * options.UnboundedRayLength,
                        ray.WavelengthNanometers, bounce, ray.DiffractionOrder, ray.Intensity));
                    break;
                }

                var hitPoint = ray.Origin + ray.Direction * nearestHit.Distance;
                output.Add(new RaySegment(ray.Origin, hitPoint, ray.WavelengthNanometers, bounce,
                    ray.DiffractionOrder, ray.Intensity));

                if (nearestHit.Kind is OpticalHitKind.Screen or OpticalHitKind.Aperture)
                {
                    break;
                }

                if (nearestHit.Kind == OpticalHitKind.ReflectionGrating)
                {
                    var grating = nearestHit.GetElement<ReflectionGratingSegment>();
                    if (bounce < options.MaximumReflections)
                    {
                        foreach (var diffractedRay in Diffract(ray, grating,
                                     options.MaximumDiffractionOrder))
                        {
                            if (output.Count + pending.Count >= options.MaximumSegments)
                            {
                                break;
                            }

                            pending.Enqueue((new Ray2D(
                                hitPoint + diffractedRay.Direction * (options.IntersectionEpsilon * 32),
                                diffractedRay.Direction,
                                ray.WavelengthNanometers,
                                diffractedRay.Order,
                                ray.Intensity * DiffractionIntensityFactor(diffractedRay.Order)),
                                bounce + 1));
                            diffractedRayCount++;
                        }
                    }

                    break;
                }


                if (nearestHit.Kind == OpticalHitKind.BeamSplitter)
                {
                    var beamSplitter = nearestHit.GetElement<BeamSplitterSegment>();
                    if (bounce < options.MaximumReflections)
                    {
                        var tangent = (beamSplitter.End - beamSplitter.Start).Normalized();
                        var reflectedDirection = ray.Direction.Reflected(tangent.Perpendicular()).Normalized();
                        var splitIntensity = ray.Intensity * 0.5;
                        var offset = options.IntersectionEpsilon * 32;

                        if (output.Count + pending.Count < options.MaximumSegments)
                        {
                            pending.Enqueue((new Ray2D(hitPoint + reflectedDirection * offset,
                                reflectedDirection, ray.WavelengthNanometers, ray.DiffractionOrder,
                                splitIntensity), bounce + 1));
                            reflectedRayCount++;
                        }

                        if (output.Count + pending.Count < options.MaximumSegments)
                        {
                            pending.Enqueue((new Ray2D(hitPoint + ray.Direction * offset,
                                ray.Direction, ray.WavelengthNanometers, ray.DiffractionOrder,
                                splitIntensity), bounce + 1));
                        }
                    }

                    break;
                }

                var nextDirection = ResolveInteraction(ray, hitPoint, nearestHit,
                    ref reflectedRayCount, ref refractedRayCount);

                ray = new Ray2D(hitPoint + nextDirection * (options.IntersectionEpsilon * 32),
                    nextDirection, ray.WavelengthNanometers, ray.DiffractionOrder, ray.Intensity);
            }
        }
    }

    private static OpticalHit? FindNearestHit(Ray2D ray, OpticalScene scene, double epsilon)
    {
        OpticalHit? nearest = null;

        void Consider(double distance, OpticalHitKind kind, object element)
        {
            if (nearest is null || distance < nearest.Value.Distance)
            {
                nearest = new OpticalHit(distance, kind, element);
            }
        }

        foreach (var mirror in scene.Mirrors)
        {
            if (TryIntersect(ray, mirror.Start, mirror.End, epsilon, out var distance))
            {
                Consider(distance, OpticalHitKind.Mirror, mirror);
            }
        }

        foreach (var mirror in scene.ConcaveSphericalMirrorElements)
        {
            if (TryIntersect(ray, mirror, epsilon, out var distance))
            {
                Consider(distance, OpticalHitKind.ConcaveSphericalMirror, mirror);
            }
        }

        foreach (var mirror in scene.ConvexSphericalMirrorElements)
        {
            if (TryIntersect(ray, mirror, epsilon, out var distance))
            {
                Consider(distance, OpticalHitKind.ConvexSphericalMirror, mirror);
            }
        }

        foreach (var lens in scene.LensElements)
        {
            if (TryIntersect(ray, lens.Start, lens.End, epsilon, out var distance))
            {
                Consider(distance, OpticalHitKind.Lens, lens);
            }
        }

        foreach (var screen in scene.ScreenElements)
        {
            if (TryIntersect(ray, screen.Start, screen.End, epsilon, out var distance))
            {
                Consider(distance, OpticalHitKind.Screen, screen);
            }
        }

        foreach (var aperture in scene.ApertureElements)
        {
            if (!TryIntersect(ray, aperture.Start, aperture.End, epsilon, out var distance))
            {
                continue;
            }

            var hitPoint = ray.Origin + ray.Direction * distance;
            if (!IsWithinOpening(hitPoint, aperture, epsilon))
            {
                Consider(distance, OpticalHitKind.Aperture, aperture);
            }
        }

        foreach (var grating in scene.ReflectionGratingElements)
        {
            if (TryIntersect(ray, grating.Start, grating.End, epsilon, out var distance))
            {
                Consider(distance, OpticalHitKind.ReflectionGrating, grating);
            }
        }

        foreach (var beamSplitter in scene.BeamSplitterElements)
        {
            if (TryIntersect(ray, beamSplitter.Start, beamSplitter.End, epsilon, out var distance))
            {
                Consider(distance, OpticalHitKind.BeamSplitter, beamSplitter);
            }
        }

        return nearest;
    }

    private static Vector2D ResolveInteraction(
        Ray2D ray,
        Vector2D hitPoint,
        OpticalHit hit,
        ref int reflectedRayCount,
        ref int refractedRayCount)
    {
        switch (hit.Kind)
        {
            case OpticalHitKind.Mirror:
                var mirror = hit.GetElement<MirrorSegment>();
                var tangent = (mirror.End - mirror.Start).Normalized();
                reflectedRayCount++;
                return ray.Direction.Reflected(tangent.Perpendicular()).Normalized();

            case OpticalHitKind.ConcaveSphericalMirror:
                var concaveMirror = hit.GetElement<ConcaveSphericalMirror>();
                var concaveNormal = (hitPoint - concaveMirror.CenterOfCurvature).Normalized();
                reflectedRayCount++;
                return ray.Direction.Reflected(concaveNormal).Normalized();

            case OpticalHitKind.ConvexSphericalMirror:
                var convexMirror = hit.GetElement<ConvexSphericalMirror>();
                var convexNormal = (hitPoint - convexMirror.CenterOfCurvature).Normalized();
                reflectedRayCount++;
                return ray.Direction.Reflected(convexNormal).Normalized();

            case OpticalHitKind.Lens:
                refractedRayCount++;
                return RefractThroughIdealLens(ray, hitPoint, hit.GetElement<LensSegment>());

            default:
                throw new InvalidOperationException($"命中类型 {hit.Kind} 不能作为连续传播交互处理。");
        }
    }

    private static IEnumerable<(Vector2D Direction, int Order)> Diffract(
        Ray2D ray,
        ReflectionGratingSegment grating,
        int maximumOrder)
    {
        var tangent = (grating.End - grating.Start).Normalized();
        var normal = tangent.Perpendicular();
        var incomingNormal = ray.Direction.Dot(normal);
        var outgoingNormalSign = incomingNormal >= 0 ? -1d : 1d;
        var incomingTangential = ray.Direction.Dot(tangent);
        var wavelengthMillimeters = Math.Max(0, ray.WavelengthNanometers) * 1e-6;
        var grooveDensity = Math.Max(1e-9, grating.GrooveDensityLinesPerMillimeter);
        var tangentialStep = wavelengthMillimeters * grooveDensity;
        var orderLimit = Math.Clamp(maximumOrder, 0, 6);

        for (var order = -orderLimit; order <= orderLimit; order++)
        {
            var outgoingTangential = incomingTangential + order * tangentialStep;
            if (Math.Abs(outgoingTangential) > 1 + 1e-12)
            {
                continue;
            }

            outgoingTangential = Math.Clamp(outgoingTangential, -1, 1);
            var outgoingNormal = outgoingNormalSign *
                                 Math.Sqrt(Math.Max(0, 1 - outgoingTangential * outgoingTangential));
            yield return ((tangent * outgoingTangential + normal * outgoingNormal).Normalized(), order);
        }
    }

    private static double DiffractionIntensityFactor(int order) =>
        Math.Max(0, 7 - Math.Abs(order)) / 8d;

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

    private static bool TryIntersect(
        Ray2D ray,
        ConcaveSphericalMirror mirror,
        double epsilon,
        out double distance) =>
        TryIntersectSpherical(ray, mirror.Vertex, mirror.CenterOfCurvature,
            mirror.ArcAngleDegrees, true, epsilon, out distance);

    private static bool TryIntersect(
        Ray2D ray,
        ConvexSphericalMirror mirror,
        double epsilon,
        out double distance) =>
        TryIntersectSpherical(ray, mirror.Vertex, mirror.CenterOfCurvature,
            mirror.ArcAngleDegrees, false, epsilon, out distance);

    private static bool TryIntersectSpherical(
        Ray2D ray,
        Vector2D vertex,
        Vector2D centerOfCurvature,
        double arcAngleDegrees,
        bool isConcave,
        double epsilon,
        out double distance)
    {
        var radius = (centerOfCurvature - vertex).Length;
        if (radius <= epsilon)
        {
            distance = 0;
            return false;
        }

        var offset = ray.Origin - centerOfCurvature;
        var projection = offset.Dot(ray.Direction);
        var discriminant = projection * projection - (offset.LengthSquared - radius * radius);
        if (discriminant < -epsilon)
        {
            distance = 0;
            return false;
        }

        var root = Math.Sqrt(Math.Max(0, discriminant));
        var first = -projection - root;
        var second = -projection + root;
        foreach (var candidate in new[] { first, second })
        {
            if (candidate <= epsilon)
            {
                continue;
            }

            var hitPoint = ray.Origin + ray.Direction * candidate;
            var normal = (hitPoint - centerOfCurvature).Normalized();
            var hitsReflectiveSide = isConcave
                ? ray.Direction.Dot(normal) > epsilon
                : ray.Direction.Dot(normal) < -epsilon;
            if (hitsReflectiveSide && IsPointOnArc(
                    hitPoint, vertex, centerOfCurvature, arcAngleDegrees))
            {
                distance = candidate;
                return true;
            }
        }

        distance = 0;
        return false;
    }

    private static bool IsPointOnArc(
        Vector2D point,
        Vector2D vertex,
        Vector2D centerOfCurvature,
        double arcAngleDegrees)
    {
        var centerAngle = Math.Atan2(
            vertex.Y - centerOfCurvature.Y,
            vertex.X - centerOfCurvature.X);
        var pointAngle = Math.Atan2(
            point.Y - centerOfCurvature.Y,
            point.X - centerOfCurvature.X);
        var difference = Math.Atan2(Math.Sin(pointAngle - centerAngle), Math.Cos(pointAngle - centerAngle));
        var halfAngle = Math.Clamp(Math.Abs(arcAngleDegrees), 1, 359.9) * Math.PI / 360;
        return Math.Abs(difference) <= halfAngle + 1e-10;
    }

    private static bool IsWithinOpening(Vector2D hitPoint, ApertureSegment aperture, double epsilon)
    {
        var edge = aperture.End - aperture.Start;
        var length = edge.Length;
        if (length <= epsilon)
        {
            return false;
        }

        var midpoint = (aperture.Start + aperture.End) / 2;
        var halfOpening = Math.Clamp(aperture.OpeningSize, 0, length) / 2;
        return Math.Abs((hitPoint - midpoint).Dot(edge / length)) <= halfOpening + epsilon;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
