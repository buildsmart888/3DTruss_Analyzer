namespace TrussAnalyzer.Core.Models;

/// <summary>
/// Represents a 3D point or vector with explicit units (meters).
/// Used for node coordinates and displacement vectors.
/// </summary>
public readonly struct Point3D
{
    /// <summary>X coordinate in meters (m)</summary>
    public double X { get; }
    
    /// <summary>Y coordinate in meters (m)</summary>
    public double Y { get; }
    
    /// <summary>Z coordinate in meters (m)</summary>
    public double Z { get; }

    public Point3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// Calculates the distance between this point and another point.
    /// Returns: distance in meters (m)
    /// </summary>
    public double DistanceTo(Point3D other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        double dz = other.Z - Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// Vector subtraction: this - other
    /// Returns: Vector from other to this (in meters)
    /// </summary>
    public Vector3D Subtract(Point3D other)
    {
        return new Vector3D(X - other.X, Y - other.Y, Z - other.Z);
    }

    public static Point3D Zero => new(0, 0, 0);

    public override string ToString() => $"({X:F4}, {Y:F4}, {Z:F4}) m";
}

/// <summary>
/// Represents a 3D vector with explicit units (typically meters for geometry, Newtons for forces).
/// </summary>
public readonly struct Vector3D
{
    /// <summary>X component (unit depends on context: m or N)</summary>
    public double X { get; }
    
    /// <summary>Y component (unit depends on context: m or N)</summary>
    public double Y { get; }
    
    /// <summary>Z component (unit depends on context: m or N)</summary>
    public double Z { get; }

    public Vector3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// Magnitude of the vector.
    /// Returns: magnitude in same units as components
    /// </summary>
    public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>
    /// Normalizes the vector to unit length.
    /// Returns: unit vector (dimensionless)
    /// </summary>
    public Vector3D Normalize()
    {
        double mag = Magnitude;
        if (mag < 1e-10)
            throw new InvalidOperationException("Cannot normalize a zero vector.");
        return new Vector3D(X / mag, Y / mag, Z / mag);
    }

    /// <summary>
    /// Dot product of two vectors.
    /// Returns: scalar (units are product of input units)
    /// </summary>
    public double Dot(Vector3D other) => X * other.X + Y * other.Y + Z * other.Z;

    /// <summary>
    /// Vector addition.
    /// </summary>
    public Vector3D Add(Vector3D other) => new(X + other.X, Y + other.Y, Z + other.Z);

    /// <summary>
    /// Scalar multiplication.
    /// </summary>
    public Vector3D Scale(double scalar) => new(X * scalar, Y * scalar, Z * scalar);

    public static Vector3D Zero => new(0, 0, 0);

    public override string ToString() => $"[{X:F4}, {Y:F4}, {Z:F4}]";
}
