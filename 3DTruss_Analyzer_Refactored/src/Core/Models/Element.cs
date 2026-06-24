namespace TrussAnalyzer.Core.Models;

/// <summary>
/// Represents a truss element (member) connecting two nodes.
/// Uses SI units throughout (meters, Newtons, Pascals).
/// </summary>
public class Element
{
    /// <summary>Unique identifier for the element</summary>
    public int Id { get; }

    /// <summary>ID of the starting node</summary>
    public int StartNodeId { get; }

    /// <summary>ID of the ending node</summary>
    public int EndNodeId { get; }

    /// <summary>Cross-sectional area (units: m²)</summary>
    public double Area { get; }

    /// <summary>Material properties</summary>
    public Material Material { get; }

    /// <summary>Length of the element (units: m)</summary>
    public double Length { get; private set; }

    /// <summary>Direction cosines (unit vector from start to end)</summary>
    public Vector3D DirectionCosines { get; private set; }

    /// <summary>Axial force in the element (units: N). Positive = Tension, Negative = Compression</summary>
    public double AxialForce { get; set; }

    /// <summary>Axial stress (units: Pa = N/m²). Positive = Tension, Negative = Compression</summary>
    public double Stress { get; set; }

    /// <summary>Strain (dimensionless)</summary>
    public double Strain { get; set; }

    /// <summary>Self-weight force per node (units: N). Calculated automatically.</summary>
    public double SelfWeightPerNode { get; private set; }

    public Element(int id, int startNodeId, int endNodeId, double area, Material material)
    {
        if (area <= 0)
            throw new ArgumentOutOfRangeException(nameof(area), "Element area must be positive.");
        if (material.YoungsModulus <= 0)
            throw new ArgumentOutOfRangeException(nameof(material), "Material Young's modulus must be positive.");

        Id = id;
        StartNodeId = startNodeId;
        EndNodeId = endNodeId;
        Area = area;
        Material = material;
        Length = 0;
        DirectionCosines = Vector3D.Zero;
        AxialForce = 0;
        Stress = 0;
        Strain = 0;
        SelfWeightPerNode = 0;
    }

    /// <summary>
    /// Calculates geometric properties based on node positions.
    /// Must be called after nodes are positioned.
    /// </summary>
    public void UpdateGeometry(Point3D startPos, Point3D endPos)
    {
        Length = startPos.DistanceTo(endPos);
        
        if (Length < 1e-10)
            throw new InvalidOperationException($"Element {Id} has zero or near-zero length.");

        var directionVector = endPos.Subtract(startPos);
        DirectionCosines = directionVector.Normalize();

        // Calculate self-weight: W = ρ × A × L × g
        // Distribute half to each node
        double gravity = 9.81; // m/s²
        double totalWeight = Material.Density * Area * Length * gravity; // Newtons
        SelfWeightPerNode = totalWeight / 2.0; // Half to each node
    }

    /// <summary>
    /// Calculates the local stiffness matrix coefficient (EA/L).
    /// Returns: stiffness coefficient (units: N/m)
    /// </summary>
    public double GetStiffnessCoefficient()
    {
        return (Material.YoungsModulus * Area) / Length;
    }

    /// <summary>
    /// Updates internal forces and stresses based on nodal displacements.
    /// </summary>
    public void UpdateForces(Vector3D startDisplacement, Vector3D endDisplacement)
    {
        // Calculate elongation: δ = (u₂ - u₁) · n
        var relativeDisplacement = endDisplacement.Subtract(startDisplacement);
        double elongation = relativeDisplacement.Dot(DirectionCosines);

        // Strain: ε = δ / L (dimensionless)
        Strain = elongation / Length;

        // Stress: σ = E × ε (Hooke's Law) (units: Pa)
        Stress = Material.YoungsModulus * Strain;

        // Axial force: F = σ × A (units: N)
        AxialForce = Stress * Area;
    }

    public override string ToString() => 
        $"Element {Id}: Node {StartNodeId} → Node {EndNodeId}, L={Length:F3}m, F={AxialForce:F1}N";
}
