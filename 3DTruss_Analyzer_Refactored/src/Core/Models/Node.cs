namespace TrussAnalyzer.Core.Models;

/// <summary>
/// Represents a node (joint) in the truss structure.
/// </summary>
public class Node
{
    /// <summary>Unique identifier for the node</summary>
    public int Id { get; }

    /// <summary>Position in 3D space (units: meters)</summary>
    public Point3D Position { get; }

    /// <summary>External forces applied to this node (units: Newtons)</summary>
    public Vector3D AppliedForce { get; set; }

    /// <summary>Boundary conditions: true if constrained in that direction</summary>
    public bool ConstraintX { get; set; }
    public bool ConstraintY { get; set; }
    public bool ConstraintZ { get; set; }

    /// <summary>Calculated displacement after analysis (units: meters)</summary>
    public Vector3D Displacement { get; set; }

    /// <summary>Reaction forces at supports (units: Newtons)</summary>
    public Vector3D ReactionForce { get; set; }

    public Node(int id, Point3D position)
    {
        Id = id;
        Position = position;
        AppliedForce = Vector3D.Zero;
        Displacement = Vector3D.Zero;
        ReactionForce = Vector3D.Zero;
        ConstraintX = false;
        ConstraintY = false;
        ConstraintZ = false;
    }

    /// <summary>
    /// Applies an external force to this node.
    /// Forces are in Newtons (N).
    /// </summary>
    public void ApplyForce(double fx, double fy, double fz)
    {
        AppliedForce = new Vector3D(fx, fy, fz);
    }

    /// <summary>
    /// Adds to existing force (for combining multiple loads).
    /// </summary>
    public void AddForce(double fx, double fy, double fz)
    {
        var current = AppliedForce;
        AppliedForce = new Vector3D(current.X + fx, current.Y + fy, current.Z + fz);
    }

    /// <summary>
    /// Resets all applied forces to zero.
    /// Used when switching between load cases.
    /// </summary>
    public void ResetForces()
    {
        AppliedForce = Vector3D.Zero;
    }

    /// <summary>
    /// Sets the displacement vector after analysis.
    /// </summary>
    public void SetDisplacement(double dx, double dy, double dz)
    {
        Displacement = new Vector3D(dx, dy, dz);
    }

    /// <summary>
    /// Sets the reaction force vector after analysis.
    /// </summary>
    public void SetReactionForce(double rx, double ry, double rz)
    {
        ReactionForce = new Vector3D(rx, ry, rz);
    }

    /// <summary>
    /// Checks if the node is constrained in any direction.
    /// </summary>
    public bool IsConstrained => ConstraintX || ConstraintY || ConstraintZ;

    public override string ToString() => $"Node {Id} at {Position}";
}
