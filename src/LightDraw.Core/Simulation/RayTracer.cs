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
                TraceSingleRay(initialRay, scene.Mirrors, scene.ConcaveSphericalMirrorElements,
                    scene.ConvexSphericalMirrorElements,
                    scene.LensElements, scene.ScreenElements,
                    scene.ApertureElements, scene.ReflectionGratingElements, scene.BeamSplitterElements,
                    options, segments,
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
        IReadOnlyList<MirrorSegment> mirrors,
        IReadOnlyList<ConcaveSphericalMirror> concaveSphericalMirrors,
        IReadOnlyList<ConvexSphericalMirror> convexSphericalMirrors,
        IReadOnlyList<LensSegment> lenses,
        IReadOnlyList<ScreenSegment> screens,
        IReadOnlyList<ApertureSegment> apertures,
        IReadOnlyList<ReflectionGratingSegment> reflectionGratings,
        IReadOnlyList<BeamSplitterSegment> beamSplitters,
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
                var nearestDistance = double.PositiveInfinity;
                MirrorSegment? nearestMirror = null;
                ConcaveSphericalMirror? nearestConcaveSphericalMirror = null;
                ConvexSphericalMirror? nearestConvexSphericalMirror = null;
                LensSegment? nearestLens = null;
                ScreenSegment? nearestScreen = null;
                ApertureSegment? nearestAperture = null;
                ReflectionGratingSegment? nearestReflectionGrating = null;
                BeamSplitterSegment? nearestBeamSplitter = null;

                foreach (var mirror in mirrors)
                {
                    if (TryIntersect(ray, mirror.Start, mirror.End, options.IntersectionEpsilon,
                            out var distance) && distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestMirror = mirror;
                        nearestConcaveSphericalMirror = null;
                        nearestConvexSphericalMirror = null;
                        nearestLens = null;
                        nearestScreen = null;
                        nearestAperture = null;
                        nearestReflectionGrating = null;
                        nearestBeamSplitter = null;
                    }
                }

                foreach (var sphericalMirror in concaveSphericalMirrors)
                {
                    if (TryIntersect(ray, sphericalMirror, options.IntersectionEpsilon,
                            out var distance) && distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestMirror = null;
                        nearestConcaveSphericalMirror = sphericalMirror;
                        nearestConvexSphericalMirror = null;
                        nearestLens = null;
                        nearestScreen = null;
                        nearestAperture = null;
                        nearestReflectionGrating = null;
                        nearestBeamSplitter = null;
                    }
                }

                foreach (var sphericalMirror in convexSphericalMirrors)
                {
                    if (TryIntersect(ray, sphericalMirror, options.IntersectionEpsilon,
                            out var distance) && distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestMirror = null;
                        nearestConcaveSphericalMirror = null;
                        nearestConvexSphericalMirror = sphericalMirror;
                        nearestLens = null;
                        nearestScreen = null;
                        nearestAperture = null;
                        nearestReflectionGrating = null;
                        nearestBeamSplitter = null;
                    }
                }

                foreach (var lens in lenses)
                {
                    if (TryIntersect(ray, lens.Start, lens.End, options.IntersectionEpsilon,
                            out var distance) && distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestMirror = null;
                        nearestConcaveSphericalMirror = null;
                        nearestConvexSphericalMirror = null;
                        nearestLens = lens;
                        nearestScreen = null;
                        nearestAperture = null;
                        nearestReflectionGrating = null;
                        nearestBeamSplitter = null;
                    }
                }

                foreach (var screen in screens)
                {
                    if (TryIntersect(ray, screen.Start, screen.End, options.IntersectionEpsilon,
                            out var distance) && distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestMirror = null;
                        nearestConcaveSphericalMirror = null;
                        nearestConvexSphericalMirror = null;
                        nearestLens = null;
                        nearestScreen = screen;
                        nearestAperture = null;
                        nearestReflectionGrating = null;
                        nearestBeamSplitter = null;
                    }
                }

                foreach (var aperture in apertures)
                {
                    if (TryIntersect(ray, aperture.Start, aperture.End, options.IntersectionEpsilon,
                            out var distance) && distance < nearestDistance)
                    {
                        var apertureHitPoint = ray.Origin + ray.Direction * distance;
                        if (IsWithinOpening(apertureHitPoint, aperture, options.IntersectionEpsilon))
                        {
                            continue;
                        }

                        nearestDistance = distance;
                        nearestMirror = null;
                        nearestConcaveSphericalMirror = null;
                        nearestConvexSphericalMirror = null;
                        nearestLens = null;
                        nearestScreen = null;
                        nearestAperture = aperture;
                        nearestReflectionGrating = null;
                        nearestBeamSplitter = null;
                    }
                }

                foreach (var grating in reflectionGratings)
                {
                    if (TryIntersect(ray, grating.Start, grating.End, options.IntersectionEpsilon,
                            out var distance) && distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestMirror = null;
                        nearestConcaveSphericalMirror = null;
                        nearestConvexSphericalMirror = null;
                        nearestLens = null;
                        nearestScreen = null;
                        nearestAperture = null;
                        nearestReflectionGrating = grating;
                        nearestBeamSplitter = null;
                    }
                }

                foreach (var beamSplitter in beamSplitters)
                {
                    if (TryIntersect(ray, beamSplitter.Start, beamSplitter.End,
                            options.IntersectionEpsilon, out var distance) && distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestMirror = null;
                        nearestConcaveSphericalMirror = null;
                        nearestConvexSphericalMirror = null;
                        nearestLens = null;
                        nearestScreen = null;
                        nearestAperture = null;
                        nearestReflectionGrating = null;
                        nearestBeamSplitter = beamSplitter;
                    }
                }

                if (nearestMirror is null && nearestConcaveSphericalMirror is null &&
                    nearestConvexSphericalMirror is null &&
                    nearestLens is null && nearestScreen is null &&
                    nearestAperture is null && nearestReflectionGrating is null && nearestBeamSplitter is null)
                {
                    output.Add(new RaySegment(ray.Origin,
                        ray.Origin + ray.Direction * options.UnboundedRayLength,
                        ray.WavelengthNanometers, bounce, ray.DiffractionOrder, ray.Intensity));
                    break;
                }

                var hitPoint = ray.Origin + ray.Direction * nearestDistance;
                output.Add(new RaySegment(ray.Origin, hitPoint, ray.WavelengthNanometers, bounce,
                    ray.DiffractionOrder, ray.Intensity));

                if (nearestScreen is not null || nearestAperture is not null)
                {
                    break;
                }

                if (nearestReflectionGrating is not null)
                {
                    if (bounce < options.MaximumReflections)
                    {
                        foreach (var diffractedRay in Diffract(ray, nearestReflectionGrating,
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


                if (nearestBeamSplitter is not null)
                {
                    if (bounce < options.MaximumReflections)
                    {
                        var tangent = (nearestBeamSplitter.End - nearestBeamSplitter.Start).Normalized();
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

                Vector2D nextDirection;
                if (nearestMirror is not null)
                {
                    var tangent = (nearestMirror.End - nearestMirror.Start).Normalized();
                    nextDirection = ray.Direction.Reflected(tangent.Perpendicular()).Normalized();
                    reflectedRayCount++;
                }
                else if (nearestConcaveSphericalMirror is not null)
                {
                    var normal = (hitPoint - nearestConcaveSphericalMirror.CenterOfCurvature).Normalized();
                    nextDirection = ray.Direction.Reflected(normal).Normalized();
                    reflectedRayCount++;
                }
                else if (nearestConvexSphericalMirror is not null)
                {
                    var normal = (hitPoint - nearestConvexSphericalMirror.CenterOfCurvature).Normalized();
                    nextDirection = ray.Direction.Reflected(normal).Normalized();
                    reflectedRayCount++;
                }
                else
                {
                    nextDirection = RefractThroughIdealLens(ray, hitPoint, nearestLens!);
                    refractedRayCount++;
                }

                ray = new Ray2D(hitPoint + nextDirection * (options.IntersectionEpsilon * 32),
                    nextDirection, ray.WavelengthNanometers, ray.DiffractionOrder, ray.Intensity);
            }
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
