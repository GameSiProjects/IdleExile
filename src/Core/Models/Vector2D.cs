using System;

namespace IdleExile.Core.Models;

/// <summary>
/// Immutable, lightweight 2D floating-point vector for pure C# simulation and spatial geometry math.
/// Strictly decoupled from Godot.Node and Godot.Vector2 to allow headless simulation,
/// unit testing, and instant offline mathematical estimations.
/// </summary>
public readonly record struct Vector2D(float X, float Y)
{
    public static readonly Vector2D Zero = new(0f, 0f);
    public static readonly Vector2D One = new(1f, 1f);
    public static readonly Vector2D UnitX = new(1f, 0f);
    public static readonly Vector2D UnitY = new(0f, 1f);

    public float Length => MathF.Sqrt(X * X + Y * Y);
    public float LengthSquared => X * X + Y * Y;

    /// <summary>
    /// Euclidean distance between this point and another point.
    /// Formula: d(P1, P2) = sqrt((x2 - x1)^2 + (y2 - y1)^2)
    /// </summary>
    public float DistanceTo(Vector2D other)
    {
        float dx = other.X - X;
        float dy = other.Y - Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Squared Euclidean distance between this point and another point.
    /// Useful for fast distance threshold comparisons without calculating square root.
    /// </summary>
    public float DistanceSquaredTo(Vector2D other)
    {
        float dx = other.X - X;
        float dy = other.Y - Y;
        return dx * dx + dy * dy;
    }

    /// <summary>
    /// Dot product of this vector and another vector.
    /// </summary>
    public float Dot(Vector2D other) => X * other.X + Y * other.Y;

    /// <summary>
    /// Returns the unit vector in the same direction, or Zero if length is practically zero.
    /// </summary>
    public Vector2D Normalized()
    {
        float len = Length;
        return len > 1e-6f ? new Vector2D(X / len, Y / len) : Zero;
    }

    /// <summary>
    /// Returns the 2D angle in radians of this vector from the positive X axis.
    /// </summary>
    public float Angle() => MathF.Atan2(Y, X);

    /// <summary>
    /// Constructs a unit vector from an angle in radians.
    /// </summary>
    public static Vector2D FromAngle(float radians) => new(MathF.Cos(radians), MathF.Sin(radians));

    /// <summary>
    /// Calculates the Euclidean distance between two vectors.
    /// </summary>
    public static float Distance(Vector2D a, Vector2D b) => a.DistanceTo(b);

    /// <summary>
    /// Calculates the dot product of two vectors.
    /// </summary>
    public static float Dot(Vector2D a, Vector2D b) => a.Dot(b);

    /// <summary>
    /// Linear interpolation between two vectors.
    /// </summary>
    public static Vector2D Lerp(Vector2D a, Vector2D b, float t)
    {
        float clampedT = Math.Clamp(t, 0f, 1f);
        return new Vector2D(a.X + (b.X - a.X) * clampedT, a.Y + (b.Y - a.Y) * clampedT);
    }

    // ─────────────────────────────────────────────
    //  Operators
    // ─────────────────────────────────────────────
    public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2D operator -(Vector2D a) => new(-a.X, -a.Y);
    public static Vector2D operator *(Vector2D a, float scalar) => new(a.X * scalar, a.Y * scalar);
    public static Vector2D operator *(float scalar, Vector2D a) => new(a.X * scalar, a.Y * scalar);
    public static Vector2D operator /(Vector2D a, float scalar) =>
        scalar != 0f ? new Vector2D(a.X / scalar, a.Y / scalar) : Zero;

    public override string ToString() => $"({X:F1}, {Y:F1})";
}
