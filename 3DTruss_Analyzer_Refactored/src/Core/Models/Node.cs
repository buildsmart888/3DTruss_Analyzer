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

    /// <summary>External moments applied to this node (units: N-m)</summary>
    public Vector3D AppliedMoment { get; set; }

    /// <summary>Boundary conditions: true if constrained in that direction</summary>
    public bool ConstraintX { get; set; }
    public bool ConstraintY { get; set; }
    public bool ConstraintZ { get; set; }
    public bool ConstraintRX { get; set; }
    public bool ConstraintRY { get; set; }
    public bool ConstraintRZ { get; set; }

    /// <summary>Calculated displacement after analysis (units: meters)</summary>
    public Vector3D Displacement { get; set; }

    /// <summary>Calculated rotations after analysis (units: radians)</summary>
    public Vector3D Rotation { get; set; }

    /// <summary>Reaction forces at supports (units: Newtons)</summary>
    public Vector3D ReactionForce { get; set; }

    /// <summary>Reaction moments at supports (units: N-m)</summary>
    public Vector3D ReactionMoment { get; set; }

    public Node(int id, Point3D position)
    {
        Id = id;
        Position = position;
        AppliedForce = Vector3D.Zero;
        AppliedMoment = Vector3D.Zero;
        Displacement = Vector3D.Zero;
        Rotation = Vector3D.Zero;
        ReactionForce = Vector3D.Zero;
        ReactionMoment = Vector3D.Zero;
        ConstraintX = false;
        ConstraintY = false;
        ConstraintZ = false;
        ConstraintRX = false;
        ConstraintRY = false;
        ConstraintRZ = false;
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
    /// Applies an external moment to this node.
    /// Moments are in Newton-meters (N-m).
    /// </summary>
    public void ApplyMoment(double mx, double my, double mz)
    {
        AppliedMoment = new Vector3D(mx, my, mz);
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
    /// Adds to existing moment.
    /// </summary>
    public void AddMoment(double mx, double my, double mz)
    {
        var current = AppliedMoment;
        AppliedMoment = new Vector3D(current.X + mx, current.Y + my, current.Z + mz);
    }

    /// <summary>
    /// Resets all applied forces to zero.
    /// Used when switching between load cases.
    /// </summary>
    public void ResetForces()
    {
        AppliedForce = Vector3D.Zero;
        AppliedMoment = Vector3D.Zero;
    }

    /// <summary>
    /// Sets the displacement vector after analysis.
    /// </summary>
    public void SetDisplacement(double dx, double dy, double dz)
    {
        Displacement = new Vector3D(dx, dy, dz);
    }

    /// <summary>
    /// Sets the rotation vector after analysis.
    /// </summary>
    public void SetRotation(double rx, double ry, double rz)
    {
        Rotation = new Vector3D(rx, ry, rz);
    }

    /// <summary>
    /// Sets the reaction force vector after analysis.
    /// </summary>
    public void SetReactionForce(double rx, double ry, double rz)
    {
        ReactionForce = new Vector3D(rx, ry, rz);
    }

    /// <summary>
    /// Sets the reaction moment vector after analysis.
    /// </summary>
    public void SetReactionMoment(double mx, double my, double mz)
    {
        ReactionMoment = new Vector3D(mx, my, mz);
    }

    /// <summary>
    /// Checks if the node is constrained in any direction.
    /// </summary>
    public bool IsConstrained => ConstraintX || ConstraintY || ConstraintZ || ConstraintRX || ConstraintRY || ConstraintRZ;

    public override string ToString() => $"Node {Id} at {Position}";
}
