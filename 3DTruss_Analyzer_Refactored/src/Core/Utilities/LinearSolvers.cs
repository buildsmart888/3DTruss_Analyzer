namespace TrussAnalyzer.Core.Utilities;

public interface ILinearSystemSolver
{
    string Name { get; }
    double[] Solve(double[,] matrix, double[] rhs);
}

public sealed class DenseLinearSystemSolver : ILinearSystemSolver
{
    public string Name => "Dense Gaussian elimination";

    public double[] Solve(double[,] matrix, double[] rhs) => Matrix.SolveAuto(matrix, rhs);
}

public sealed class SparsePlaceholderSolver : ILinearSystemSolver
{
    public string Name => "Sparse solver placeholder";

    public double[] Solve(double[,] matrix, double[] rhs)
    {
        // Placeholder keeps behavior deterministic while preserving the future sparse-solver interface.
        return Matrix.SolveAuto(matrix, rhs);
    }
}
