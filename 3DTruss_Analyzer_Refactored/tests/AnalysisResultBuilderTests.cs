namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Analysis;
using TrussAnalyzer.Core.Models;
using Xunit;

public class AnalysisResultBuilderTests
{
    [Fact]
    public void BuildNodeResults_RecoversConstrainedReactionsAndUpdatesModelNodeState()
    {
        var model = CreateFrameModel();
        var dofIndexer = new DofIndexer(model.Nodes);
        var builder = CreateBuilder(model, dofIndexer);
        var stiffness = CreateDiagonalMatrix(dofIndexer.TotalDof, 2);
        var loads = new double[dofIndexer.TotalDof];
        var displacements = new double[dofIndexer.TotalDof];
        loads[0] = 1;
        displacements[0] = 3;
        displacements[6] = 0.004;

        var nodeResults = builder.BuildNodeResults(stiffness, loads, displacements);

        Assert.Equal(5, nodeResults.Single(result => result.NodeId == 1).ReactionForce.X, precision: 12);
        Assert.Equal(0.004, nodeResults.Single(result => result.NodeId == 2).Displacement.X, precision: 12);
        Assert.Equal(0.004, model.Nodes.Single(node => node.Id == 2).Displacement.X, precision: 12);
    }

    [Fact]
    public void BuildElementAndAnalysisResults_ReturnsStableResultDtoAndDiagnostics()
    {
        var model = CreateFrameModel();
        var dofIndexer = new DofIndexer(model.Nodes);
        var builder = CreateBuilder(model, dofIndexer);
        var stiffness = CreateDiagonalMatrix(dofIndexer.TotalDof, 1);
        var loads = new double[dofIndexer.TotalDof];
        var displacements = new double[dofIndexer.TotalDof];
        var nodeResults = builder.BuildNodeResults(stiffness, loads, displacements);
        var elementResults = builder.BuildElementResults(displacements, new Dictionary<int, double[]>());

        var result = builder.BuildAnalysisResult(
            "Default",
            nodeResults,
            elementResults,
            new List<DesignCheckResult>(),
            loads,
            nonZeroStiffnessEntries: dofIndexer.TotalDof,
            solverName: "Dense Gaussian Elimination");

        Assert.Equal("Default", result.LoadCaseName);
        Assert.Equal(2, result.NodeResults.Count);
        Assert.Single(result.ElementResults);
        Assert.True(result.Equilibrium.IsSatisfied);
        Assert.Equal(1.0 / dofIndexer.TotalDof, result.Diagnostics.MatrixDensity, precision: 12);
    }

    private static AnalysisResultBuilder CreateBuilder(StructuralModel model, DofIndexer dofIndexer)
    {
        var stiffnessProvider = new FrameElementStiffnessProvider();
        return new AnalysisResultBuilder(
            model,
            dofIndexer,
            new ReactionRecoveryService(),
            new ElementForceRecoveryService(model, dofIndexer, stiffnessProvider),
            new EquilibriumCheckService(),
            new SolverDiagnosticsService());
    }

    private static double[,] CreateDiagonalMatrix(int size, double value)
    {
        var matrix = new double[size, size];
        for (int index = 0; index < size; index++)
            matrix[index, index] = value;
        return matrix;
    }

    private static StructuralModel CreateFrameModel()
    {
        var model = new StructuralModel();
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0)) { ConstraintX = true });
        model.Nodes.Add(new Node(2, new Point3D(2, 0, 0)));
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(Section.Generic(1, "Frame", 0.003, 4e-6, 6e-6, 2e-6));
        model.Elements.Add(new FrameElement3D(1, 1, 2, materialId: 1, sectionId: 1));
        return model;
    }
}
