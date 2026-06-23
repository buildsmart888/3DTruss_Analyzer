using System;

namespace _3DTrussAnalyzer.Models
{
    /// <summary>
    /// Represents a node in the 3D truss structure
    /// A node is a connection point for truss elements with coordinates, constraints, and applied loads
    /// </summary>
    public class Node
    {
        /// <summary>
        /// Unique identifier for this node
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Label/name for display purposes (optional)
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// 3D coordinates of the node
        /// Units: meters (m)
        /// </summary>
        public Point3D Coordinates { get; set; }

        /// <summary>
        /// Boundary constraints at this node
        /// </summary>
        public Constraint Constraint { get; set; } = new Constraint();

        /// <summary>
        /// External force applied at this node
        /// Units: Newtons (N)
        /// Positive direction: +X, +Y, +Z
        /// </summary>
        public Vector3D AppliedForce { get; set; } = new Vector3D();

        /// <summary>
        /// Calculated displacement after analysis
        /// Units: meters (m)
        /// </summary>
        public Vector3D Displacement { get; set; } = new Vector3D();

        /// <summary>
        /// Calculated reaction force after analysis (if constrained)
        /// Units: Newtons (N)
        /// </summary>
        public Vector3D ReactionForce { get; set; } = new Vector3D();

        /// <summary>
        /// List of element IDs connected to this node
        /// </summary>
        public List<int> ConnectedElementIds { get; set; } = new List<int>();

        /// <summary>
        /// Creates a new node at specified coordinates
        /// </summary>
        public Node(int id, double x, double y, double z)
        {
            Id = id;
            Coordinates = new Point3D(x, y, z);
        }

        /// <summary>
        /// Creates a new node at specified coordinates with label
        /// </summary>
        public Node(int id, double x, double y, double z, string label) : this(id, x, y, z)
        {
            Label = label;
        }

        /// <summary>
        /// Checks if this node has any constraints
        /// </summary>
        public bool IsConstrained => Constraint.HasAnyRestraint;

        /// <summary>
        /// Checks if this node has any applied external force
        /// </summary>
        public bool HasAppliedForce => Math.Abs(AppliedForce.X) > 1e-10 || 
                                       Math.Abs(AppliedForce.Y) > 1e-10 || 
                                       Math.Abs(AppliedForce.Z) > 1e-10;

        /// <summary>
        /// Sets constraint for this node
        /// </summary>
        public void SetConstraint(bool xFixed, bool yFixed, bool zFixed)
        {
            Constraint = new Constraint(xFixed, yFixed, zFixed);
        }

        /// <summary>
        /// Applies an external force at this node
        /// </summary>
        public void ApplyForce(double fx, double fy, double fz)
        {
            AppliedForce = new Vector3D(fx, fy, fz);
        }

        /// <summary>
        /// Resets analysis results (displacement and reaction)
        /// </summary>
        public void ResetAnalysisResults()
        {
            Displacement = new Vector3D();
            ReactionForce = new Vector3D();
        }

        public override string ToString()
        {
            return $"Node {Id} '{Label ?? "N/A"}' at {Coordinates}";
        }
    }
}
