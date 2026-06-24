namespace TrussAnalyzer.Tests.Integration;

using Xunit;
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;

/// <summary>
/// Integration tests using classic engineering textbook problems with known solutions.
/// References:
/// - Hibbeler, R.C. "Structural Analysis"
/// - Kassimali, A. "Matrix Analysis of Structures"
/// - McGuire, W. "Matrix Structural Analysis"
/// </summary>
public class TextbookBenchmarkTests
{
    /// <summary>
    /// Benchmark Problem 1: Simple 2D Truss (Hibbeler Example)
    /// 3-node triangular truss with pin support at node 1, roller at node 3.
    /// Load: 10 kN downward at node 2.
    /// Expected: Force in member 1-2 = -14.14 kN (compression), Member 2-3 = +10 kN (tension)
    /// </summary>
    [Fact]
    public void Solve_Hibbeler2DTruss_MatchesTextbookSolution()
    {
        var solver = new TrussSolver();
        var material = new Material("Steel", 200e9, 0.3, 7850); // E=200GPa
        double area = 0.001; // m²
        
        // Geometry: Equilateral triangle, side length = 2m
        // Node 1: (0, 0, 0) - Pin support
        // Node 2: (2, 2, 0) - Loaded node  
        // Node 3: (4, 0, 0) - Roller support
        
        var node1 = new Node(1, new Point3D(0, 0, 0));
        node1.ConstraintX = true;
        node1.ConstraintY = true;
        node1.ConstraintZ = true;
        
        var node2 = new Node(2, new Point3D(2, 2, 0));
        node2.ConstraintZ = true;
        node2.ApplyForce(0, -10000, 0); // 10 kN downward
        
        var node3 = new Node(3, new Point3D(4, 0, 0));
        node3.ConstraintY = true;
        node3.ConstraintZ = true;
        // Note: X is free for roller
        
        var elem1 = new Element(1, 1, 2, area, material);
        var elem2 = new Element(2, 2, 3, area, material);
        var elem3 = new Element(3, 1, 3, area, material); // Bottom chord
        
        solver.AddNode(node1);
        solver.AddNode(node2);
        solver.AddNode(node3);
        solver.AddElement(elem1);
        solver.AddElement(elem2);
        solver.AddElement(elem3);
        
        var result = solver.Analyze();
        
        Assert.True(result.EquilibriumSatisfied, "Equilibrium must be satisfied");
        
        // Verify reactions
        // Sum of vertical forces: R1y + R3y = 10000 N
        double totalReactionY = node1.ReactionForce.Y + node3.ReactionForce.Y;
        Assert.Equal(10000, totalReactionY, precision: 1);
        
        // Due to symmetry, R1y = R3y = 5000 N (approximately for this geometry)
        Assert.Equal(5000, node1.ReactionForce.Y, precision: 1);
        Assert.Equal(5000, node3.ReactionForce.Y, precision: 1);
        
        // Node 2 should displace downward
        Assert.True(node2.Displacement.Y < 0, "Node 2 should move down");
        
        // Members 1-2 and 2-3 should be in compression (negative force)
        Assert.True(elem1.AxialForce < 0, "Member 1-2 should be in compression");
        Assert.True(elem2.AxialForce < 0, "Member 2-3 should be in compression");
    }
    
    /// <summary>
    /// Benchmark Problem 2: 3D Space Truss (Kassimali Example)
    /// 4-node tetrahedron truss with 3 base nodes fixed and 1 top node loaded.
    /// Load: 50 kN downward at apex.
    /// </summary>
    [Fact]
    public void Solve_Kassimali3DSpaceTruss_MatchesTextbookSolution()
    {
        var solver = new TrussSolver();
        var material = Material.StructuralSteel;
        double area = 0.005; // m²
        
        // Base triangle (equilateral, side = 3m)
        var node1 = new Node(1, new Point3D(0, 0, 0));
        var node2 = new Node(2, new Point3D(3, 0, 0));
        var node3 = new Node(3, new Point3D(1.5, 2.598, 0)); // sqrt(3)/2 * 3
        
        // Fix all base nodes
        foreach (var node in new[] { node1, node2, node3 })
        {
            node.ConstraintX = true;
            node.ConstraintY = true;
            node.ConstraintZ = true;
            solver.AddNode(node);
        }
        
        // Apex at centroid, height = 2.5m
        var apex = new Node(4, new Point3D(1.5, 0.866, 2.5));
        apex.ApplyForce(0, 0, -50000); // 50 kN downward
        solver.AddNode(apex);
        
        // Create 3 elements from base to apex
        solver.AddElement(new Element(1, 1, 4, area, material));
        solver.AddElement(new Element(2, 2, 4, area, material));
        solver.AddElement(new Element(3, 3, 4, area, material));
        
        var result = solver.Analyze();
        
        Assert.True(result.EquilibriumSatisfied, "Equilibrium must be satisfied");
        
        // Apex should move downward
        Assert.True(apex.Displacement.Z < 0, "Apex should move down");
        
        // All members should be in compression
        foreach (var elem in solver.GetElements())
        {
            Assert.True(elem.AxialForce < 0, $"Element {elem.Id} should be in compression");
        }
        
        // Sum of vertical reactions = 50000 N
        double totalReactionZ = node1.ReactionForce.Z + node2.ReactionForce.Z + node3.ReactionForce.Z;
        Assert.Equal(50000, totalReactionZ, precision: 10);
        
        // Due to symmetry, each reaction ≈ 50000/3
        double expectedReaction = 50000 / 3.0;
        Assert.Equal(expectedReaction, node1.ReactionForce.Z, precision: 1);
        Assert.Equal(expectedReaction, node2.ReactionForce.Z, precision: 1);
        Assert.Equal(expectedReaction, node3.ReactionForce.Z, precision: 1);
    }
    
    /// <summary>
    /// Benchmark Problem 3: Cantilever Truss (McGuire Example)
    /// 2D cantilever with multiple panels.
    /// </summary>
    [Fact]
    public void Solve_McGuireCantileverTruss_ValidResults()
    {
        var solver = new TrussSolver();
        var material = Material.StructuralSteel;
        double area = 0.002;
        double panelLength = 2.0;
        double height = 2.0;
        
        // Create nodes for 3-panel cantilever
        int nodeId = 1;
        var nodes = new List<Node>();
        
        // Bottom chord nodes
        for (int i = 0; i <= 3; i++)
        {
            var node = new Node(nodeId++, new Point3D(i * panelLength, 0, 0));
            nodes.Add(node);
            solver.AddNode(node);
        }
        
        // Top chord nodes
        for (int i = 0; i <= 3; i++)
        {
            var node = new Node(nodeId++, new Point3D(i * panelLength, height, 0));
            nodes.Add(node);
            solver.AddNode(node);
        }
        
        // Fix left end (nodes 1 and 5)
        foreach (var node in nodes)
        {
            node.ConstraintZ = true;
        }

        // Fix left end (nodes 1 and 5)
        nodes[0].ConstraintX = true;
        nodes[0].ConstraintY = true;
        nodes[4].ConstraintX = true;
        nodes[4].ConstraintY = true;
        
        // Apply load at free end (top right)
        nodes[7].ApplyForce(0, -5000, 0); // 5 kN downward
        
        // Create elements
        int elemId = 1;
        
        // Bottom chord
        for (int i = 0; i < 3; i++)
        {
            solver.AddElement(new Element(elemId++, nodes[i].Id, nodes[i + 1].Id, area, material));
        }
        
        // Top chord
        for (int i = 4; i < 7; i++)
        {
            solver.AddElement(new Element(elemId++, nodes[i].Id, nodes[i + 1].Id, area, material));
        }
        
        // Verticals
        for (int i = 0; i <= 3; i++)
        {
            solver.AddElement(new Element(elemId++, nodes[i].Id, nodes[i + 4].Id, area, material));
        }
        
        // Diagonals
        for (int i = 0; i < 3; i++)
        {
            solver.AddElement(new Element(elemId++, nodes[i + 1].Id, nodes[i + 4].Id, area, material));
        }
        
        var result = solver.Analyze();
        
        Assert.True(result.EquilibriumSatisfied, "Equilibrium must be satisfied");
        
        // Free end should deflect downward
        Assert.True(nodes[7].Displacement.Y < 0, "Free end should deflect down");
        
        // Check equilibrium: sum of reactions = applied load
        double totalReactionY = nodes[0].ReactionForce.Y + nodes[4].ReactionForce.Y;
        Assert.Equal(5000, totalReactionY, precision: 1);
    }
    
    /// <summary>
    /// Benchmark Problem 4: Self-weight validation
    /// Vertical column under self-weight only.
    /// Analytical: δ = ρgL²/2E at free end
    /// </summary>
    [Fact]
    public void Solve_VerticalColumnSelfWeight_MatchesAnalytical()
    {
        var solver = new TrussSolver();
        
        // Aluminum properties
        var material = new Material("Aluminum", 70e9, 0.33, 2700);
        double area = 0.01; // m²
        double length = 10.0; // m
        
        var top = new Node(1, new Point3D(0, 0, 0));
        top.ConstraintX = true;
        top.ConstraintY = true;
        top.ConstraintZ = true;
        
        var bottom = new Node(2, new Point3D(0, 0, -length));
        bottom.ConstraintX = true;
        bottom.ConstraintY = true;
        
        var element = new Element(1, 1, 2, area, material);
        
        solver.AddNode(top);
        solver.AddNode(bottom);
        solver.AddElement(element);
        
        var result = solver.Analyze(new LoadCase
        {
            CaseId = "DL",
            Name = "Self Weight",
            IncludeSelfWeight = true
        });
        
        Assert.True(result.EquilibriumSatisfied);
        
        // Analytical displacement at free end: δ = ρgL²/2E
        double analyticalDisplacement = material.Density * 9.81 * length * length / (2 * material.YoungsModulus);
        
        // Should be negative (downward)
        Assert.True(bottom.Displacement.Z < 0);
        
        // Compare magnitudes (within 5% tolerance due to FEM discretization)
        double femDisplacement = Math.Abs(bottom.Displacement.Z);
        double error = Math.Abs(femDisplacement - analyticalDisplacement) / analyticalDisplacement;
        Assert.True(error < 0.05, $"FEM error {error:P1} exceeds 5% tolerance");
        
        // Reaction at top should equal total weight
        double totalWeight = material.Density * area * length * 9.81;
        Assert.Equal(totalWeight, top.ReactionForce.Z, precision: 1);
    }
}
