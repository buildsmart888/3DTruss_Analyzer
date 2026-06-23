namespace TrussAnalyzer.Tests;

using Xunit;
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;

/// <summary>
/// Tests for the complete truss solver using benchmark problems with known solutions.
/// </summary>
public class TrussSolverTests
{
    /// <summary>
    /// Test: Simple 2D bar in tension (1 element, 2 nodes).
    /// Fixed at node 1, force applied at node 2 in X direction.
    /// Analytical solution: δ = FL/AE, σ = F/A
    /// </summary>
    [Fact]
    public void Solve_SimpleBarInTension_ReturnsCorrectDisplacement()
    {
        // Arrange
        var solver = new TrussSolver();
        
        // Material: Steel, E = 200 GPa, A = 0.001 m², L = 2 m, F = 10000 N
        var material = Material.StructuralSteel;
        double area = 0.001;  // m²
        double length = 2.0;  // m
        double force = 10000; // N
        
        // Expected displacement: δ = FL/AE = 10000 * 2 / (0.001 * 200e9) = 0.0001 m = 0.1 mm
        double expectedDisplacement = force * length / (area * material.YoungsModulus);
        
        // Expected stress: σ = F/A = 10000 / 0.001 = 10 MPa
        double expectedStress = force / area;

        // Create nodes
        var node1 = new Node(1, new Point3D(0, 0, 0));
        node1.ConstraintX = true;
        node1.ConstraintY = true;
        node1.ConstraintZ = true;
        
        var node2 = new Node(2, new Point3D(length, 0, 0));
        node2.ApplyForce(force, 0, 0);

        // Create element
        var element = new Element(1, 1, 2, area, material);

        // Add to solver
        solver.AddNode(node1);
        solver.AddNode(node2);
        solver.AddElement(element);

        // Act
        var result = solver.Analyze();

        // Assert
        Assert.True(result.EquilibriumSatisfied, "Equilibrium should be satisfied");
        
        // Check displacement at node 2 (only X should move)
        Assert.Equal(expectedDisplacement, node2.Displacement.X, precision: 8);
        Assert.Equal(0, node2.Displacement.Y, precision: 8);
        Assert.Equal(0, node2.Displacement.Z, precision: 8);
        
        // Check element stress
        Assert.Equal(expectedStress, element.Stress, precision: 6);
        
        // Check axial force
        Assert.Equal(force, element.AxialForce, precision: 6);
        
        // Check reaction at node 1 (should be -force)
        Assert.Equal(-force, node1.ReactionForce.X, precision: 6);
    }

    /// <summary>
    /// Test: Vertical bar under self-weight only.
    /// Fixed at top, hanging vertically.
    /// </summary>
    [Fact]
    public void Solve_VerticalBarSelfWeight_CalculatesCorrectly()
    {
        // Arrange
        var solver = new TrussSolver();
        
        var material = Material.StructuralSteel;
        double area = 0.01;   // m²
        double length = 10.0; // m
        
        // Create nodes
        var node1 = new Node(1, new Point3D(0, 0, 0)); // Top (fixed)
        node1.ConstraintX = true;
        node1.ConstraintY = true;
        node1.ConstraintZ = true;
        
        var node2 = new Node(2, new Point3D(0, 0, -length)); // Bottom (free)

        // Create element
        var element = new Element(1, 1, 2, area, material);

        solver.AddNode(node1);
        solver.AddNode(node2);
        solver.AddElement(element);

        // Act
        var result = solver.Analyze();

        // Assert
        Assert.True(result.EquilibriumSatisfied, "Equilibrium should be satisfied");
        
        // Node 2 should displace downward (negative Z)
        Assert.True(node2.Displacement.Z < 0, "Bottom node should move down due to self-weight");
        
        // Top node should have upward reaction equal to total weight
        double totalWeight = material.Density * area * length * 9.81;
        Assert.Equal(totalWeight, node1.ReactionForce.Z, precision: 3);
    }

    /// <summary>
    /// Test: Simple 3D tripod structure.
    /// Three elements meeting at a central loaded node.
    /// </summary>
    [Fact]
    public void Solve_TripodStructure_ReturnsValidResults()
    {
        // Arrange
        var solver = new TrussSolver();
        var material = Material.StructuralSteel;
        double area = 0.002; // m²
        
        // Tripod: three legs from base nodes to central top node
        var baseNodes = new[]
        {
            new Node(1, new Point3D(2, 0, 0)),
            new Node(2, new Point3D(-1, 1.732, 0)),
            new Node(3, new Point3D(-1, -1.732, 0))
        };
        
        // Fix all base nodes
        foreach (var node in baseNodes)
        {
            node.ConstraintX = true;
            node.ConstraintY = true;
            node.ConstraintZ = true;
            solver.AddNode(node);
        }
        
        // Central top node at height 3m
        var topNode = new Node(4, new Point3D(0, 0, 3));
        topNode.ApplyForce(0, 0, -5000); // 5 kN downward load
        solver.AddNode(topNode);
        
        // Create three elements
        for (int i = 0; i < 3; i++)
        {
            var element = new Element(i + 1, i + 1, 4, area, material);
            solver.AddElement(element);
        }

        // Act
        var result = solver.Analyze();

        // Assert
        Assert.True(result.EquilibriumSatisfied, "Equilibrium should be satisfied");
        
        // Top node should move downward
        Assert.True(topNode.Displacement.Z < 0, "Top node should move down under load");
        
        // All elements should be in compression (negative force)
        foreach (var element in solver.GetElements())
        {
            Assert.True(element.AxialForce < 0, $"Element {element.Id} should be in compression");
        }
        
        // Sum of vertical reactions should equal applied load
        double totalReactionZ = baseNodes.Sum(n => n.ReactionForce.Z);
        Assert.Equal(5000, totalReactionZ, precision: 2);
    }

    /// <summary>
    /// Test: Unstable structure should throw exception.
    /// </summary>
    [Fact]
    public void Solve_UnstableStructure_ThrowsException()
    {
        // Arrange: Single free-floating element (no constraints)
        var solver = new TrussSolver();
        
        var node1 = new Node(1, new Point3D(0, 0, 0));
        var node2 = new Node(2, new Point3D(1, 0, 0));
        var element = new Element(1, 1, 2, 0.001, Material.StructuralSteel);
        
        solver.AddNode(node1);
        solver.AddNode(node2);
        solver.AddElement(element);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => solver.Analyze());
    }
}
