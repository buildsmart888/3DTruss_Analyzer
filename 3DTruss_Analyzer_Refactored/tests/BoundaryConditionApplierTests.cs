namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Analysis;
using Xunit;

public class BoundaryConditionApplierTests
{
    [Fact]
    public void Apply_ConstrainedDof_ZeroesRowAndColumnAndPreservesOtherEntries()
    {
        var stiffness = new double[,]
        {
            { 10, 11, 12 },
            { 20, 21, 22 },
            { 30, 31, 32 }
        };
        var loadVector = new[] { 100.0, 200.0, 300.0 };

        new BoundaryConditionApplier().Apply(stiffness, loadVector, new[] { 1 });

        Assert.Equal(new[] { 0.0, 1.0, 0.0 }, GetRow(stiffness, 1));
        Assert.Equal(new[] { 0.0, 1.0, 0.0 }, GetColumn(stiffness, 1));
        Assert.Equal(10.0, stiffness[0, 0]);
        Assert.Equal(12.0, stiffness[0, 2]);
        Assert.Equal(30.0, stiffness[2, 0]);
        Assert.Equal(32.0, stiffness[2, 2]);
        Assert.Equal(new[] { 100.0, 0.0, 300.0 }, loadVector);
    }

    [Fact]
    public void Apply_MultipleConstraints_AppliesEachConstraint()
    {
        var stiffness = new double[,]
        {
            { 1, 2, 3 },
            { 4, 5, 6 },
            { 7, 8, 9 }
        };
        var loadVector = new[] { 1.0, 2.0, 3.0 };

        new BoundaryConditionApplier().Apply(stiffness, loadVector, new[] { 0, 2 });

        Assert.Equal(1.0, stiffness[0, 0]);
        Assert.Equal(1.0, stiffness[2, 2]);
        Assert.Equal(5.0, stiffness[1, 1]);
        Assert.Equal(0.0, stiffness[0, 1]);
        Assert.Equal(0.0, stiffness[1, 0]);
        Assert.Equal(0.0, stiffness[1, 2]);
        Assert.Equal(0.0, stiffness[2, 1]);
        Assert.Equal(new[] { 0.0, 2.0, 0.0 }, loadVector);
    }

    [Fact]
    public void Apply_PrescribedDisplacement_TransfersConstraintTermsToFreeDofLoadVector()
    {
        var stiffness = new double[,]
        {
            { 10, -10 },
            { -10, 10 }
        };
        var loadVector = new[] { 0.0, 0.0 };

        new BoundaryConditionApplier().Apply(stiffness, loadVector, new Dictionary<int, double> { [1] = 2.0 });

        Assert.Equal(new[] { 10.0, 0.0 }, GetRow(stiffness, 0));
        Assert.Equal(new[] { 0.0, 1.0 }, GetRow(stiffness, 1));
        Assert.Equal(new[] { 20.0, 2.0 }, loadVector);
    }

    [Fact]
    public void Apply_MismatchedMatrixAndLoadVector_ThrowsClearError()
    {
        var stiffness = new double[2, 2];
        var loadVector = new double[3];

        var error = Assert.Throws<ArgumentException>(() =>
            new BoundaryConditionApplier().Apply(stiffness, loadVector, new[] { 0 }));

        Assert.Contains("match the load vector", error.Message);
    }

    private static double[] GetRow(double[,] matrix, int row) =>
        Enumerable.Range(0, matrix.GetLength(1)).Select(column => matrix[row, column]).ToArray();

    private static double[] GetColumn(double[,] matrix, int column) =>
        Enumerable.Range(0, matrix.GetLength(0)).Select(row => matrix[row, column]).ToArray();
}
