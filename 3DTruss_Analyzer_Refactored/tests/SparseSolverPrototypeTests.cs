namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Utilities;
using Xunit;

public class SparseSolverPrototypeTests
{
    [Fact]
    public void SparseMatrix_FromDense_StoresOnlyNonZeroValuesAndRoundTrips()
    {
        var dense = new[,]
        {
            { 10.0, 0.0, 2.0 },
            { 0.0, 5.0, 0.0 },
            { 2.0, 0.0, 3.0 }
        };

        var sparse = SparseMatrix.FromDense(dense);
        var roundTrip = sparse.ToDense();

        Assert.Equal(5, sparse.NonZeroCount);
        Assert.Equal(10.0, sparse[0, 0], precision: 12);
        Assert.Equal(0.0, sparse[1, 0], precision: 12);
        for (int row = 0; row < dense.GetLength(0); row++)
        {
            for (int column = 0; column < dense.GetLength(1); column++)
                Assert.Equal(dense[row, column], roundTrip[row, column], precision: 12);
        }
    }

    [Fact]
    public void SparsePrototypeLinearSystemSolver_MatchesDenseSolver()
    {
        var matrix = new[,]
        {
            { 4.0, 1.0, 0.0 },
            { 1.0, 3.0, 1.0 },
            { 0.0, 1.0, 2.0 }
        };
        var rhs = new[] { 1.0, 2.0, 3.0 };

        var dense = new DenseLinearSystemSolver().Solve(matrix, rhs);
        var sparse = new SparsePrototypeLinearSystemSolver().Solve(matrix, rhs);

        Assert.Equal(dense[0], sparse[0], precision: 12);
        Assert.Equal(dense[1], sparse[1], precision: 12);
        Assert.Equal(dense[2], sparse[2], precision: 12);
    }

    [Fact]
    public void StructuralSolver_WithSparsePrototypeReportsSolverPathInDiagnostics()
    {
        var model = CreateAxialFrameModel();

        var result = new StructuralSolver(model, new SparsePrototypeLinearSystemSolver()).Analyze();

        Assert.Contains("Sparse prototype adapter", result.Diagnostics.SolverName);
        Assert.Contains("fallback", result.Diagnostics.SolverName);
        Assert.True(result.Equilibrium.IsSatisfied);
    }

    [Fact]
    public void StructuralSolver_DefaultPathRemainsDense()
    {
        var model = CreateAxialFrameModel();

        var result = new StructuralSolver(model).Analyze();

        Assert.Equal("Dense Gaussian elimination", result.Diagnostics.SolverName);
    }

    private static StructuralModel CreateAxialFrameModel()
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(new Section
        {
            Id = 1,
            Name = "Axial section",
            Area = 0.003,
            Iy = 6e-6,
            Iz = 8e-6,
            J = 2e-6,
            Width = 0.2,
            Depth = 0.3
        });
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
        model.Nodes.Single(n => n.Id == 2).ApplyForce(10_000, 0, 0);
        model.Elements.Add(new FrameElement3D(1, 1, 2, materialId: 1, sectionId: 1));
        return model;
    }
}
