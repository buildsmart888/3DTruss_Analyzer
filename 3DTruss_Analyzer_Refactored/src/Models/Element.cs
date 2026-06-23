using System;

namespace _3DTrussAnalyzer.Models
{
    /// <summary>
    /// Represents a 3D truss element (bar member)
    /// A truss element can only carry axial forces (tension or compression)
    /// </summary>
    public class Element
    {
        /// <summary>
        /// Unique identifier for this element
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Label/name for display purposes (optional)
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// ID of the start node
        /// </summary>
        public int StartNodeId { get; set; }

        /// <summary>
        /// ID of the end node
        /// </summary>
        public int EndNodeId { get; set; }

        /// <summary>
        /// Cross-sectional area of the element
        /// Units: m²
        /// Typical values:
        /// - Steel pipe: 0.001 to 0.01 m²
        /// - Steel rod: 0.0001 to 0.005 m²
        /// </summary>
        public double Area { get; set; }

        /// <summary>
        /// Material properties of the element
        /// </summary>
        public Material Material { get; set; }

        /// <summary>
        /// Calculated length of the element (auto-calculated from node coordinates)
        /// Units: meters (m)
        /// </summary>
        public double Length { get; private set; }

        /// <summary>
        /// Calculated axial force after analysis
        /// Units: Newtons (N)
        /// Positive = Tension, Negative = Compression
        /// </summary>
        public double AxialForce { get; set; }

        /// <summary>
        /// Calculated axial stress after analysis
        /// Units: Pascals (Pa) or N/m²
        /// Positive = Tensile stress, Negative = Compressive stress
        /// </summary>
        public double AxialStress { get; set; }

        /// <summary>
        /// Calculated axial deformation after analysis
        /// Units: meters (m)
        /// Positive = Elongation, Negative = Shortening
        /// </summary>
        public double AxialDeformation { get; set; }

        /// <summary>
        /// Direction cosines (unit vector from start to end node)
        /// </summary>
        public Vector3D DirectionCosines { get; private set; }

        /// <summary>
        /// Creates a new truss element between two nodes
        /// </summary>
        public Element(int id, int startNodeId, int endNodeId, double area, Material material)
        {
            Id = id;
            StartNodeId = startNodeId;
            EndNodeId = endNodeId;
            Area = area;
            Material = material;
        }

        /// <summary>
        /// Creates a new truss element with label
        /// </summary>
        public Element(int id, int startNodeId, int endNodeId, double area, Material material, string label) 
            : this(id, startNodeId, endNodeId, area, material)
        {
            Label = label;
        }

        /// <summary>
        /// Updates the element geometry based on node coordinates
        /// Must be called before analysis to ensure correct length and direction
        /// </summary>
        public void UpdateGeometry(Point3D startPoint, Point3D endPoint)
        {
            Length = Point3D.Distance(startPoint, endPoint);
            
            if (Length < 1e-10)
                throw new InvalidOperationException($"Element {Id} has zero or near-zero length");

            var directionVector = Vector3D.FromPoints(startPoint, endPoint);
            DirectionCosines = directionVector.Normalize();
        }

        /// <summary>
        /// Calculates the element stiffness coefficient (k = AE/L)
        /// Units: N/m
        /// </summary>
        public double StiffnessCoefficient
        {
            get { return (Material.YoungsModulus * Area) / Length; }
        }

        /// <summary>
        /// Calculates self-weight force at each node
        /// Total weight = ρ × A × L × g
        /// Distributed equally to both nodes (w/2 at each end)
        /// Returns force vector in global coordinates
        /// Units: Newtons (N)
        /// </summary>
        public Vector3D SelfWeightForce
        {
            get
            {
                double totalWeight = Material.Density * Area * Length * 9.81;
                // Weight acts downward (negative Z direction typically)
                // For general case, we return magnitude; direction applied in solver
                return new Vector3D(0, 0, -totalWeight / 2.0); // Half at each node
            }
        }

        /// <summary>
        /// Checks if the element is valid for analysis
        /// </summary>
        public bool IsValid => Length > 1e-10 && Area > 0 && Material.YoungsModulus > 0;

        public override string ToString()
        {
            return $"Element {Id} '{Label ?? "N/A"}': Node {StartNodeId} → Node {EndNodeId}, L = {Length:F3} m";
        }
    }
}
