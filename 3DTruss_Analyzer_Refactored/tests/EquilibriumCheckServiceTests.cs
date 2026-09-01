namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Analysis;
using TrussAnalyzer.Core.Models;
using Xunit;

public class EquilibriumCheckServiceTests
{
    [Fact]
    public void Calculate_ReturnsBalancedForceResidualAndExistingToleranceRule()
    {
        var nodes = new[]
        {
            new Node(10, new Point3D(0, 0, 0)),
            new Node(20, new Point3D(1, 0, 0))
        };
        var nodeResults = new[]
        {
            new StructuralNodeResult { NodeId = 10, ReactionForce = new Vector3D(10, 0, 5) },
            new StructuralNodeResult { NodeId = 20, ReactionForce = new Vector3D(0, 7, 0) }
        };
        var loads = new double[12];
        loads[0] = -10;
        loads[2] = -5;
        loads[7] = -7;

        var equilibrium = new EquilibriumCheckService().Calculate(nodes, nodeResults, loads, new DofIndexer(nodes));

        Assert.Equal(0, equilibrium.SumFX, precision: 12);
        Assert.Equal(0, equilibrium.SumFY, precision: 12);
        Assert.Equal(0, equilibrium.SumFZ, precision: 12);
        Assert.Equal(1e-6, equilibrium.Tolerance, precision: 12);
        Assert.True(equilibrium.IsSatisfied);
    }

    [Fact]
    public void Calculate_UsesAppliedLoadAndReactionMagnitudeForLargeModelTolerance()
    {
        var nodes = new[] { new Node(1, new Point3D(0, 0, 0)) };
        var nodeResults = new[]
        {
            new StructuralNodeResult { NodeId = 1, ReactionForce = new Vector3D(-1_000_000, 0, 0) }
        };
        var loads = new double[6];
        loads[0] = 1_000_000;

        var equilibrium = new EquilibriumCheckService().Calculate(nodes, nodeResults, loads, new DofIndexer(nodes));

        Assert.Equal(0.002, equilibrium.Tolerance, precision: 12);
    }

    [Fact]
    public void Calculate_MissingNodeResult_ThrowsClearError()
    {
        var nodes = new[] { new Node(1, new Point3D(0, 0, 0)) };

        var error = Assert.Throws<ArgumentException>(() =>
            new EquilibriumCheckService().Calculate(nodes, Array.Empty<StructuralNodeResult>(), new double[6], new DofIndexer(nodes)));

        Assert.Equal("nodeResults", error.ParamName);
    }
}
