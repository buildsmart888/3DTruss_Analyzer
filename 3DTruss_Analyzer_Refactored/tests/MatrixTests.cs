namespace TrussAnalyzer.Tests;

using Xunit;
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Utilities;

/// <summary>
/// Tests for the Matrix utility class.
/// </summary>
public class MatrixTests
{
    [Fact]
    public void Solve_IdentityMatrix_ReturnsRHS()
    {
        // Arrange: Identity matrix 3x3
        var A = new double[,] 
        { 
            { 1, 0, 0 }, 
            { 0, 1, 0 }, 
            { 0, 0, 1 } 
        };
        var b = new double[] { 5, 10, 15 };

        // Act
        var x = Matrix.Solve(A, b);

        // Assert
        Assert.Equal(5, x[0], precision: 10);
        Assert.Equal(10, x[1], precision: 10);
        Assert.Equal(15, x[2], precision: 10);
    }

    [Fact]
    public void Solve_SimpleSystem_ReturnsCorrectSolution()
    {
        // Arrange: 2x + y = 5, x - y = 1
        // Solution: x = 2, y = 1
        var A = new double[,] 
        { 
            { 2, 1 }, 
            { 1, -1 } 
        };
        var b = new double[] { 5, 1 };

        // Act
        var x = Matrix.Solve(A, b);

        // Assert
        Assert.Equal(2, x[0], precision: 10);
        Assert.Equal(1, x[1], precision: 10);
    }

    [Fact]
    public void Solve_RequiresPivoting_ReturnsCorrectSolution()
    {
        // Arrange: System requiring row swap for stability
        // 0.0001x + y = 1, x + y = 2
        // Solution: x ≈ 1.0001, y ≈ 0.9999
        var A = new double[,] 
        { 
            { 0.0001, 1 }, 
            { 1, 1 } 
        };
        var b = new double[] { 1, 2 };

        // Act
        var x = Matrix.Solve(A, b);

        // Assert (approximate)
        Assert.Equal(1.0001, x[0], precision: 4);
        Assert.Equal(0.9999, x[1], precision: 4);
    }

    [Fact]
    public void Multiply_CorrectDimensions_ReturnsCorrectResult()
    {
        // Arrange
        var A = new double[,] 
        { 
            { 1, 2 }, 
            { 3, 4 } 
        };
        var B = new double[,] 
        { 
            { 5, 6 }, 
            { 7, 8 } 
        };

        // Act
        var C = Matrix.Multiply(A, B);

        // Assert: C = [[1*5+2*7, 1*6+2*8], [3*5+4*7, 3*6+4*8]] = [[19, 22], [43, 50]]
        Assert.Equal(19, C[0, 0], precision: 10);
        Assert.Equal(22, C[0, 1], precision: 10);
        Assert.Equal(43, C[1, 0], precision: 10);
        Assert.Equal(50, C[1, 1], precision: 10);
    }

    [Fact]
    public void Transpose_SquareMatrix_ReturnsCorrectTranspose()
    {
        // Arrange
        var A = new double[,] 
        { 
            { 1, 2, 3 }, 
            { 4, 5, 6 }, 
            { 7, 8, 9 } 
        };

        // Act
        var AT = Matrix.Transpose(A);

        // Assert
        Assert.Equal(1, AT[0, 0]); Assert.Equal(4, AT[0, 1]); Assert.Equal(7, AT[0, 2]);
        Assert.Equal(2, AT[1, 0]); Assert.Equal(5, AT[1, 1]); Assert.Equal(8, AT[1, 2]);
        Assert.Equal(3, AT[2, 0]); Assert.Equal(6, AT[2, 1]); Assert.Equal(9, AT[2, 2]);
    }
}
