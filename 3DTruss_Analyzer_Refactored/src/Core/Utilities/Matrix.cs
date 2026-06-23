namespace TrussAnalyzer.Core.Utilities;

/// <summary>
/// Matrix utilities for structural analysis.
/// Implements Gaussian elimination with partial pivoting for solving linear systems.
/// </summary>
public static class Matrix
{
    /// <summary>
    /// Solves the linear system Ax = b using Gaussian elimination with partial pivoting.
    /// Returns: solution vector x
    /// </summary>
    /// <param name="A">Coefficient matrix (will be modified)</param>
    /// <param name="b">Right-hand side vector (will be modified)</param>
    /// <returns>Solution vector x</returns>
    public static double[] Solve(double[,] A, double[] b)
    {
        int n = b.Length;
        
        if (A.GetLength(0) != n || A.GetLength(1) != n)
            throw new ArgumentException("Matrix A must be square and match vector b size.");

        // Create copies to avoid modifying originals
        var matrix = (double[,])A.Clone();
        var rhs = (double[])b.Clone();

        // Forward elimination with partial pivoting
        for (int col = 0; col < n; col++)
        {
            // Find pivot
            int maxRow = col;
            double maxValue = Math.Abs(matrix[col, col]);
            
            for (int row = col + 1; row < n; row++)
            {
                if (Math.Abs(matrix[row, col]) > maxValue)
                {
                    maxValue = Math.Abs(matrix[row, col]);
                    maxRow = row;
                }
            }

            // Check for singular matrix
            if (maxValue < 1e-12)
                throw new InvalidOperationException("Matrix is singular or nearly singular. Structure may be unstable.");

            // Swap rows
            if (maxRow != col)
            {
                for (int j = 0; j < n; j++)
                {
                    double temp = matrix[col, j];
                    matrix[col, j] = matrix[maxRow, j];
                    matrix[maxRow, j] = temp;
                }
                double tempB = rhs[col];
                rhs[col] = rhs[maxRow];
                rhs[maxRow] = tempB;
            }

            // Eliminate column
            for (int row = col + 1; row < n; row++)
            {
                double factor = matrix[row, col] / matrix[col, col];
                for (int j = col; j < n; j++)
                {
                    matrix[row, j] -= factor * matrix[col, j];
                }
                rhs[row] -= factor * rhs[col];
            }
        }

        // Back substitution
        var x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = 0;
            for (int j = i + 1; j < n; j++)
            {
                sum += matrix[i, j] * x[j];
            }
            x[i] = (rhs[i] - sum) / matrix[i, i];
        }

        return x;
    }

    /// <summary>
    /// Creates a zero matrix of specified size.
    /// </summary>
    public static double[,] Create(int rows, int cols)
    {
        return new double[rows, cols];
    }

    /// <summary>
    /// Creates an identity matrix of specified size.
    /// </summary>
    public static double[,] CreateIdentity(int size)
    {
        var matrix = new double[size, size];
        for (int i = 0; i < size; i++)
        {
            matrix[i, i] = 1.0;
        }
        return matrix;
    }

    /// <summary>
    /// Multiplies two matrices: C = A × B
    /// </summary>
    public static double[,] Multiply(double[,] A, double[,] B)
    {
        int rowsA = A.GetLength(0);
        int colsA = A.GetLength(1);
        int rowsB = B.GetLength(0);
        int colsB = B.GetLength(1);

        if (colsA != rowsB)
            throw new ArgumentException("Matrix dimensions don't match for multiplication.");

        var C = new double[rowsA, colsB];
        for (int i = 0; i < rowsA; i++)
        {
            for (int j = 0; j < colsB; j++)
            {
                double sum = 0;
                for (int k = 0; k < colsA; k++)
                {
                    sum += A[i, k] * B[k, j];
                }
                C[i, j] = sum;
            }
        }
        return C;
    }

    /// <summary>
    /// Multiplies a matrix by a scalar.
    /// </summary>
    public static double[,] Scale(double[,] A, double scalar)
    {
        int rows = A.GetLength(0);
        int cols = A.GetLength(1);
        var result = new double[rows, cols];
        
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                result[i, j] = A[i, j] * scalar;
            }
        }
        return result;
    }

    /// <summary>
    /// Adds two matrices: C = A + B
    /// </summary>
    public static double[,] Add(double[,] A, double[,] B)
    {
        int rows = A.GetLength(0);
        int cols = A.GetLength(1);

        if (rows != B.GetLength(0) || cols != B.GetLength(1))
            throw new ArgumentException("Matrix dimensions must match for addition.");

        var C = new double[rows, cols];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                C[i, j] = A[i, j] + B[i, j];
            }
        }
        return C;
    }

    /// <summary>
    /// Transposes a matrix.
    /// </summary>
    public static double[,] Transpose(double[,] A)
    {
        int rows = A.GetLength(0);
        int cols = A.GetLength(1);
        var result = new double[cols, rows];
        
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                result[j, i] = A[i, j];
            }
        }
        return result;
    }
}
