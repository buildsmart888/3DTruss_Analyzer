using System;

namespace _3DTrussAnalyzer.Utilities
{
    /// <summary>
    /// Basic matrix class for structural analysis calculations
    /// Supports essential operations for FEM implementation
    /// </summary>
    public class Matrix
    {
        private readonly double[,] _data;

        public int Rows { get; }
        public int Cols { get; }

        /// <summary>
        /// Gets or sets the value at the specified position
        /// </summary>
        public double this[int row, int col]
        {
            get => _data[row, col];
            set => _data[row, col] = value;
        }

        /// <summary>
        /// Creates a new matrix with specified dimensions
        /// </summary>
        public Matrix(int rows, int cols)
        {
            if (rows <= 0 || cols <= 0)
                throw new ArgumentException("Matrix dimensions must be positive");
            
            Rows = rows;
            Cols = cols;
            _data = new double[rows, cols];
        }

        /// <summary>
        /// Creates a matrix from a 2D array
        /// </summary>
        public Matrix(double[,] data)
        {
            Rows = data.GetLength(0);
            Cols = data.GetLength(1);
            _data = (double[,])data.Clone();
        }

        /// <summary>
        /// Creates an identity matrix of specified size
        /// </summary>
        public static Matrix Identity(int size)
        {
            var matrix = new Matrix(size, size);
            for (int i = 0; i < size; i++)
                matrix[i, i] = 1.0;
            return matrix;
        }

        /// <summary>
        /// Matrix addition
        /// </summary>
        public static Matrix operator +(Matrix a, Matrix b)
        {
            if (a.Rows != b.Rows || a.Cols != b.Cols)
                throw new ArgumentException("Matrix dimensions must match for addition");

            var result = new Matrix(a.Rows, a.Cols);
            for (int i = 0; i < a.Rows; i++)
                for (int j = 0; j < a.Cols; j++)
                    result[i, j] = a[i, j] + b[i, j];

            return result;
        }

        /// <summary>
        /// Matrix subtraction
        /// </summary>
        public static Matrix operator -(Matrix a, Matrix b)
        {
            if (a.Rows != b.Rows || a.Cols != b.Cols)
                throw new ArgumentException("Matrix dimensions must match for subtraction");

            var result = new Matrix(a.Rows, a.Cols);
            for (int i = 0; i < a.Rows; i++)
                for (int j = 0; j < a.Cols; j++)
                    result[i, j] = a[i, j] - b[i, j];

            return result;
        }

        /// <summary>
        /// Matrix multiplication
        /// </summary>
        public static Matrix operator *(Matrix a, Matrix b)
        {
            if (a.Cols != b.Rows)
                throw new ArgumentException($"Matrix dimensions incompatible for multiplication: {a.Rows}x{a.Cols} and {b.Rows}x{b.Cols}");

            var result = new Matrix(a.Rows, b.Cols);
            for (int i = 0; i < a.Rows; i++)
                for (int j = 0; j < b.Cols; j++)
                    for (int k = 0; k < a.Cols; k++)
                        result[i, j] += a[i, k] * b[k, j];

            return result;
        }

        /// <summary>
        /// Scalar multiplication
        /// </summary>
        public static Matrix operator *(Matrix m, double scalar)
        {
            var result = new Matrix(m.Rows, m.Cols);
            for (int i = 0; i < m.Rows; i++)
                for (int j = 0; j < m.Cols; j++)
                    result[i, j] = m[i, j] * scalar;

            return result;
        }

        /// <summary>
        /// Returns the transpose of this matrix
        /// </summary>
        public Matrix Transpose()
        {
            var result = new Matrix(Cols, Rows);
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Cols; j++)
                    result[j, i] = _data[i, j];

            return result;
        }

        /// <summary>
        /// Returns a string representation of the matrix
        /// </summary>
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Matrix [{Rows}x{Cols}]:");
            for (int i = 0; i < Rows; i++)
            {
                sb.Append("  ");
                for (int j = 0; j < Cols; j++)
                    sb.Append($"{_data[i, j],12:E4} ");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// Creates a copy of this matrix
        /// </summary>
        public Matrix Copy()
        {
            return new Matrix((double[,])_data.Clone());
        }
    }

    /// <summary>
    /// Matrix solver utilities for linear systems
    /// </summary>
    public static class MatrixSolver
    {
        /// <summary>
        /// Solves Ax = b using Gaussian elimination with partial pivoting
        /// Returns true if successful, false if matrix is singular
        /// </summary>
        public static bool GaussianElimination(Matrix A, Matrix b, Matrix x)
        {
            int n = A.Rows;
            if (A.Cols != n || b.Rows != n || b.Cols != 1 || x.Rows != n || x.Cols != 1)
                throw new ArgumentException("Invalid matrix dimensions for solving");

            // Create augmented matrix [A|b]
            var aug = new double[n, n + 1];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    aug[i, j] = A[i, j];
                aug[i, n] = b[i, 0];
            }

            // Forward elimination with partial pivoting
            for (int k = 0; k < n; k++)
            {
                // Find pivot
                int maxRow = k;
                double maxValue = Math.Abs(aug[k, k]);
                for (int i = k + 1; i < n; i++)
                {
                    if (Math.Abs(aug[i, k]) > maxValue)
                    {
                        maxValue = Math.Abs(aug[i, k]);
                        maxRow = i;
                    }
                }

                // Check for singularity
                if (maxValue < 1e-12)
                    return false;

                // Swap rows
                if (maxRow != k)
                {
                    for (int j = k; j <= n; j++)
                    {
                        double temp = aug[k, j];
                        aug[k, j] = aug[maxRow, j];
                        aug[maxRow, j] = temp;
                    }
                }

                // Eliminate column
                for (int i = k + 1; i < n; i++)
                {
                    double factor = aug[i, k] / aug[k, k];
                    for (int j = k; j <= n; j++)
                        aug[i, j] -= factor * aug[k, j];
                }
            }

            // Back substitution
            for (int i = n - 1; i >= 0; i--)
            {
                double sum = 0;
                for (int j = i + 1; j < n; j++)
                    sum += aug[i, j] * x[j, 0];
                
                x[i, 0] = (aug[i, n] - sum) / aug[i, i];

                // Check for NaN or Infinity
                if (double.IsNaN(x[i, 0]) || double.IsInfinity(x[i, 0]))
                    return false;
            }

            return true;
        }
    }
}
