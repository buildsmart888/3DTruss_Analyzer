using System;

namespace _3DTrussAnalyzer.Models
{
    /// <summary>
    /// Represents a 3D point in space with coordinates (X, Y, Z)
    /// </summary>
    public class Point3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3D() : this(0, 0, 0) { }

        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Calculates the distance between two 3D points
        /// </summary>
        public static double Distance(Point3D p1, Point3D p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double dz = p2.Z - p1.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }
    }

    /// <summary>
    /// Represents a 3D vector with components (X, Y, Z)
    /// Used for forces, displacements, and directions
    /// </summary>
    public class Vector3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Vector3D() : this(0, 0, 0) { }

        public Vector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Creates a vector from two points
        /// </summary>
        public static Vector3D FromPoints(Point3D start, Point3D end)
        {
            return new Vector3D(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
        }

        /// <summary>
        /// Calculates the magnitude (length) of the vector
        /// </summary>
        public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>
        /// Returns the unit vector (normalized direction)
        /// </summary>
        public Vector3D Normalize()
        {
            double mag = Magnitude;
            if (mag < 1e-10)
                throw new InvalidOperationException("Cannot normalize a zero vector");
            return new Vector3D(X / mag, Y / mag, Z / mag);
        }

        /// <summary>
        /// Vector addition
        /// </summary>
        public static Vector3D operator +(Vector3D v1, Vector3D v2)
        {
            return new Vector3D(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
        }

        /// <summary>
        /// Scalar multiplication
        /// </summary>
        public static Vector3D operator *(Vector3D v, double scalar)
        {
            return new Vector3D(v.X * scalar, v.Y * scalar, v.Z * scalar);
        }

        public override string ToString()
        {
            return $"[{X}, {Y}, {Z}]";
        }
    }
}
