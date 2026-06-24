namespace TrussAnalyzer.Tests.Integration;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.IO;
using TrussAnalyzer.Core.Models;
using Xunit;

public class RegressionTests
{
    [Fact]
    public void Analyze_UsesExistingNodeForces_WhenNoLoadCaseIsProvided()
    {
        var solver = CreateStableAxialBar(1000);

        var result = solver.Analyze();
        var loadedNode = result.Nodes.Single(n => n.Id == 2);

        Assert.Equal(1000, loadedNode.AppliedForce.X, precision: 6);
        Assert.True(loadedNode.Displacement.X > 0);
        Assert.True(result.EquilibriumSatisfied);
    }

    [Fact]
    public void AnalyzeMultipleLoadCases_ReturnsIndependentResultSnapshots()
    {
        var solver = CreateStableAxialBar(0);
        var loadCases = new List<LoadCase>
        {
            new()
            {
                CaseId = "L1",
                Name = "Load 1",
                NodeForces = new Dictionary<int, ForceVector> { [2] = new(1000, 0, 0) }
            },
            new()
            {
                CaseId = "L2",
                Name = "Load 2",
                NodeForces = new Dictionary<int, ForceVector> { [2] = new(2000, 0, 0) }
            }
        };

        var results = solver.AnalyzeMultipleLoadCases(loadCases);

        double firstDisplacement = results[0].Nodes.Single(n => n.Id == 2).Displacement.X;
        double secondDisplacement = results[1].Nodes.Single(n => n.Id == 2).Displacement.X;

        Assert.True(secondDisplacement > firstDisplacement);
        Assert.Equal(firstDisplacement * 2, secondDisplacement, precision: 10);
    }

    [Fact]
    public void AnalyzeLoadCombinations_IncludesFactoredSelfWeight()
    {
        var solver = new TrussSolver();
        var material = Material.StructuralSteel;
        double area = 0.01;
        double length = 10;

        var top = new Node(1, new Point3D(0, 0, 0))
        {
            ConstraintX = true,
            ConstraintY = true,
            ConstraintZ = true
        };
        var bottom = new Node(2, new Point3D(0, 0, -length))
        {
            ConstraintX = true,
            ConstraintY = true
        };

        solver.AddNode(top);
        solver.AddNode(bottom);
        solver.AddElement(new Element(1, 1, 2, area, material));

        var loadCases = new Dictionary<string, LoadCase>
        {
            ["DL"] = new() { CaseId = "DL", Name = "Dead Load", IncludeSelfWeight = true }
        };
        var combinations = new List<LoadCombination>
        {
            new()
            {
                CombinationId = "C1",
                Name = "1.4D",
                LoadCases = new Dictionary<string, double> { ["DL"] = 1.4 }
            }
        };

        var result = solver.AnalyzeLoadCombinations(combinations, loadCases).Single();

        double expectedReaction = material.Density * area * length * 9.81 * 1.4;
        Assert.Equal(expectedReaction, result.Nodes.Single(n => n.Id == 1).ReactionForce.Z, precision: 3);
    }

    [Fact]
    public void LoadCombination_Throws_WhenReferencedLoadCaseIsMissing()
    {
        var combination = new LoadCombination
        {
            CombinationId = "C1",
            Name = "Missing",
            LoadCases = new Dictionary<string, double> { ["MISSING"] = 1.0 }
        };

        Assert.Throws<InvalidOperationException>(() =>
            combination.CalculateCombinedForces(new Dictionary<string, LoadCase>()));
    }

    [Fact]
    public void StructureImporterExporter_JsonRoundTrip_PreservesSolvableModel()
    {
        var solver = CreateStableAxialBar(5000);

        string json = StructureImporterExporter.ExportToJson(solver);
        var imported = StructureImporterExporter.ImportFromJson(json);
        var result = imported.Analyze();

        Assert.Equal(2, imported.GetNodes().Count);
        Assert.Single(imported.GetElements());
        Assert.True(result.EquilibriumSatisfied);
        Assert.Equal(5000, result.Nodes.Single(n => n.Id == 2).AppliedForce.X, precision: 6);
    }

    [Fact]
    public void SafetyChecks_FlagOverstressedElement()
    {
        var solver = CreateStableAxialBar(500_000);

        var result = solver.Analyze();
        var check = result.SafetyChecks.ElementChecks.Single();

        Assert.True(check.UtilizationRatio > 1.0);
        Assert.False(check.IsPassing);
        Assert.Equal("NG", check.Status);
    }

    [Fact]
    public void ValidateModel_WarnsForPlanarNodeWithFreeZ()
    {
        var solver = new TrussSolver();
        solver.AddNode(new Node(1, new Point3D(0, 0, 0)) { ConstraintX = true, ConstraintY = true, ConstraintZ = true });
        solver.AddNode(new Node(2, new Point3D(1, 0, 0)));
        solver.AddElement(new Element(1, 1, 2, 0.001, Material.StructuralSteel));

        var messages = solver.ValidateModel();

        Assert.Contains(messages, m => m.Severity == "Warning" && m.Message.Contains("Z is unconstrained"));
    }

    [Fact]
    public void ValidateModel_WarnsForLargeDenseSolve()
    {
        var solver = new TrussSolver();
        for (int i = 1; i <= 101; i++)
        {
            solver.AddNode(new Node(i, new Point3D(i, 0, 0))
            {
                ConstraintX = i == 1,
                ConstraintY = true,
                ConstraintZ = true
            });
        }

        solver.AddElement(new Element(1, 1, 2, 0.001, Material.StructuralSteel));

        var messages = solver.ValidateModel();

        Assert.Contains(messages, m => m.Severity == "Warning" && m.Message.Contains("dense matrix solver"));
    }

    private static TrussSolver CreateStableAxialBar(double forceX)
    {
        var solver = new TrussSolver();
        var node1 = new Node(1, new Point3D(0, 0, 0))
        {
            ConstraintX = true,
            ConstraintY = true,
            ConstraintZ = true
        };
        var node2 = new Node(2, new Point3D(2, 0, 0))
        {
            ConstraintY = true,
            ConstraintZ = true
        };
        node2.ApplyForce(forceX, 0, 0);

        solver.AddNode(node1);
        solver.AddNode(node2);
        solver.AddElement(new Element(1, 1, 2, 0.001, Material.StructuralSteel));

        return solver;
    }
}
