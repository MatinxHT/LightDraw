using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;

namespace LightDraw.Rendering.Skia.Optics;

internal sealed partial class SceneEditor
{
    private bool HasUsableLength(Vector2D start, Vector2D end) =>
        (end - start).Length >= 4 / _zoom;

    private static Vector2D RotationHandle(Vector2D start, Vector2D end)
    {
        var midpoint = (start + end) / 2;
        return midpoint + (end - start).Normalized().Perpendicular() * RotationHandleOffset;
    }

    private static Vector2D SphericalMirrorRotationHandle(
        Vector2D vertex, Vector2D centerOfCurvature) =>
        vertex + (centerOfCurvature - vertex).Normalized() * RotationHandleOffset;

    private static Vector2D PointLightRotationHandle(LightSource source) =>
        source.Position + Vector2D.FromAngle(source.DirectionDegrees * Math.PI / 180) *
        RotationHandleOffset;

    private static bool TryRotateSegmentFromHandle(
        Vector2D start, Vector2D end, Vector2D handlePosition,
        out Vector2D rotatedStart, out Vector2D rotatedEnd)
    {
        var midpoint = (start + end) / 2;
        var handleDirection = (handlePosition - midpoint).Normalized();
        if (handleDirection.LengthSquared <= 1e-12)
        {
            rotatedStart = start;
            rotatedEnd = end;
            return false;
        }

        var tangent = -handleDirection.Perpendicular();
        var halfLength = (end - start).Length / 2;
        rotatedStart = midpoint - tangent * halfLength;
        rotatedEnd = midpoint + tangent * halfLength;
        return true;
    }

    private static bool IsValidIndex<T>(int index, T[] items) =>
        index >= 0 && index < items.Length;

    private static (Vector2D Start, Vector2D End) RotateSegment(
        Vector2D start,
        Vector2D end,
        double radians)
    {
        var midpoint = (start + end) / 2;
        var halfLength = (end - start).Length / 2;
        var offset = Vector2D.FromAngle(radians) * halfLength;
        return (midpoint - offset, midpoint + offset);
    }

    private static (Vector2D Start, Vector2D End) ResizeSegment(
        Vector2D start,
        Vector2D end,
        double length)
    {
        var midpoint = (start + end) / 2;
        var offset = (end - start).Normalized() * (length / 2);
        return (midpoint - offset, midpoint + offset);
    }

    private static double SegmentAngleDegrees(Vector2D start, Vector2D end) =>
        NormalizeDegrees(Math.Atan2(end.Y - start.Y, end.X - start.X) * 180 / Math.PI);

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static double NormalizeSignedDegrees(double degrees)
    {
        var normalized = NormalizeDegrees(degrees);
        return normalized > 180 ? normalized - 360 : normalized;
    }

    private static double DistanceToSegment(Vector2D point, Vector2D start, Vector2D end)
    {
        var edge = end - start;
        if (edge.LengthSquared <= 1e-12)
        {
            return (point - start).Length;
        }

        var ratio = Math.Clamp((point - start).Dot(edge) / edge.LengthSquared, 0, 1);
        return (point - (start + edge * ratio)).Length;
    }

    private static double DistanceToArc(Vector2D point, ConcaveSphericalMirror mirror) =>
        DistanceToArc(point, mirror.Vertex, mirror.CenterOfCurvature, mirror.ArcAngleDegrees);

    private static double DistanceToArc(
        Vector2D point,
        Vector2D vertex,
        Vector2D centerOfCurvature,
        double arcAngleDegrees)
    {
        var radius = (centerOfCurvature - vertex).Length;
        if (radius <= 1e-12)
        {
            return (point - vertex).Length;
        }

        var centerAngle = Math.Atan2(
            vertex.Y - centerOfCurvature.Y,
            vertex.X - centerOfCurvature.X);
        var pointAngle = Math.Atan2(
            point.Y - centerOfCurvature.Y,
            point.X - centerOfCurvature.X);
        var difference = Math.Atan2(Math.Sin(pointAngle - centerAngle), Math.Cos(pointAngle - centerAngle));
        var halfAngle = Math.Clamp(Math.Abs(arcAngleDegrees), 1, 359.9) * Math.PI / 360;
        if (Math.Abs(difference) <= halfAngle)
        {
            return Math.Abs((point - centerOfCurvature).Length - radius);
        }

        var endpointA = centerOfCurvature + Vector2D.FromAngle(centerAngle - halfAngle) * radius;
        var endpointB = centerOfCurvature + Vector2D.FromAngle(centerAngle + halfAngle) * radius;
        return Math.Min((point - endpointA).Length, (point - endpointB).Length);
    }

    private static double DirectionDegrees(Vector2D direction) =>
        Math.Atan2(direction.Y, direction.X) * 180 / Math.PI;
}
