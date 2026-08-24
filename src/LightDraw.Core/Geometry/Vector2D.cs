namespace LightDraw.Core.Geometry;

public readonly record struct Vector2D(double X, double Y)
{
    public static Vector2D Zero => new(0, 0);

    public double Length => Math.Sqrt(LengthSquared);

    public double LengthSquared => X * X + Y * Y;

    public Vector2D Normalized()
    {
        var length = Length;
        return length <= 1e-12 ? Zero : this / length;
    }

    public double Dot(Vector2D other) => X * other.X + Y * other.Y;

    public double Cross(Vector2D other) => X * other.Y - Y * other.X;

    public Vector2D Perpendicular() => new(-Y, X);

    public Vector2D Reflected(Vector2D unitNormal) =>
        this - 2 * Dot(unitNormal) * unitNormal;

    public static Vector2D FromAngle(double radians) =>
        new(Math.Cos(radians), Math.Sin(radians));

    public static Vector2D operator +(Vector2D left, Vector2D right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static Vector2D operator -(Vector2D left, Vector2D right) =>
        new(left.X - right.X, left.Y - right.Y);

    public static Vector2D operator -(Vector2D value) => new(-value.X, -value.Y);

    public static Vector2D operator *(Vector2D value, double scale) =>
        new(value.X * scale, value.Y * scale);

    public static Vector2D operator *(double scale, Vector2D value) => value * scale;

    public static Vector2D operator /(Vector2D value, double scale) =>
        new(value.X / scale, value.Y / scale);
}
