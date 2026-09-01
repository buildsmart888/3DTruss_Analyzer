namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Analysis;
using TrussAnalyzer.Core.Models;
using Xunit;

public class SolverDiagnosticsServiceTests
{
    [Fact]
    public void Build_ReturnsExpectedMetricsAndDenseWarning()
    {
        var nodes = new[]
        {
            new Node(1, new Point3D(0, 0, 0)) { ConstraintX = true, ConstraintY = true },
            new Node(2, new Point3D(1, 0, 0))
        };
        var nodeResults = new[]
        {
            new StructuralNodeResult
            {
                ReactionForce = new Vector3D(3, 4, 0),
                ReactionMoment = new Vector3D(0, 0, 12)
            }
        };
        var equilibrium = new EquilibriumCheck(0.01, 0, 0, 0.1);

        var diagnostics = new SolverDiagnosticsService(denseSolverWarningDof: 10).Build(
            new DofIndexer(nodes),
            elementCount: 4,
            nonZeroStiffnessEntries: 36,
            solverName: "Dense Gaussian Elimination",
            originalLoadVector: new[] { 3.0, -4.0, 0.0 },
            nodeResults,
            equilibrium);

        Assert.Equal(12, diagnostics.TotalDof);
        Assert.Equal(2, diagnostics.ConstrainedDof);
        Assert.Equal(4, diagnostics.ElementCount);
        Assert.Equal(0.25, diagnostics.MatrixDensity, precision: 12);
        Assert.Equal(7, diagnostics.AppliedLoadMagnitude);
        Assert.Equal(17, diagnostics.ReactionMagnitude);
        Assert.Equal(equilibrium.ResidualMagnitude, diagnostics.EquilibriumResidualMagnitude);
        Assert.True(diagnostics.DenseSolverWarning);
        Assert.Contains("sparse solver", diagnostics.Notes);
    }
}
