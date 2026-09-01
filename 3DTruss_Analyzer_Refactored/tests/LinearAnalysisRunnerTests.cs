namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Analysis;
using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Utilities;
using Xunit;

public class LinearAnalysisRunnerTests
{
    [Fact]
    public void RunLoadCase_AssemblesSelectedLoadAndReturnsSolveSnapshot()
    {
        var model = CreateAxialModel();
        model.LoadCases.Add(new LoadCase { CaseId = "DL", Name = "Dead" });
        model.Loads.Add(new NodalLoad { LoadCaseId = "DL", NodeId = 2, Force = new Vector3D(1_000, 0, 0) });

        var result = CreateRunner(model).RunLoadCase("dl");

        Assert.Equal("Dead", result.LoadCaseName);
        Assert.Equal(1_000, result.OriginalLoadVector[6], precision: 12);
        Assert.True(result.NonZeroStiffnessEntries > 0);
        Assert.Equal("Dense Gaussian elimination", result.SolverName);
        Assert.Equal(1_000 * 2 / (200e9 * 0.003), result.GlobalDisplacements[6], precision: 12);
    }

    [Fact]
    public void RunCombination_AssemblesFactoredLoadCasesWithExistingName()
    {
        var model = CreateAxialModel();
        model.LoadCases.Add(new LoadCase { CaseId = "DL", Name = "Dead" });
        model.LoadCases.Add(new LoadCase { CaseId = "LL", Name = "Live" });
        model.Loads.Add(new NodalLoad { LoadCaseId = "DL", NodeId = 2, Force = new Vector3D(1_000, 0, 0) });
        model.Loads.Add(new NodalLoad { LoadCaseId = "LL", NodeId = 2, Force = new Vector3D(2_000, 0, 0) });
        model.LoadCombinations.Add(new LoadCombination
        {
            CombinationId = "STR",
            Name = "1.2D + 1.6L",
            LoadCases = new Dictionary<string, double> { ["DL"] = 1.2, ["LL"] = 1.6 }
        });

        var result = CreateRunner(model).RunCombination("STR");

        Assert.Equal("1.2D + 1.6L", result.LoadCaseName);
        Assert.Equal(4_400, result.OriginalLoadVector[6], precision: 12);
        Assert.Equal(4_400 * 2 / (200e9 * 0.003), result.GlobalDisplacements[6], precision: 12);
    }

    private static LinearAnalysisRunner CreateRunner(StructuralModel model)
    {
        var dofIndexer = new DofIndexer(model.Nodes);
        var stiffnessProvider = new FrameElementStiffnessProvider();
        return new LinearAnalysisRunner(
            model,
            dofIndexer,
            stiffnessProvider,
            new GlobalStiffnessAssembler(),
            new LoadVectorAssembler(model, dofIndexer),
            new BoundaryConditionApplier(),
            new DenseLinearSystemSolver());
    }

    private static StructuralModel CreateAxialModel()
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(Section.Generic(1, "Axial", 0.003, 4e-6, 6e-6, 2e-6));
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0))
        {
            ConstraintX = true,
            ConstraintY = true,
            ConstraintZ = true,
            ConstraintRX = true,
            ConstraintRY = true,
            ConstraintRZ = true
        });
        model.Nodes.Add(new Node(2, new Point3D(2, 0, 0))
        {
            ConstraintY = true,
            ConstraintZ = true,
            ConstraintRX = true,
            ConstraintRY = true,
            ConstraintRZ = true
        });
        model.Elements.Add(new FrameElement3D(1, 1, 2, materialId: 1, sectionId: 1));
        return model;
    }
}
