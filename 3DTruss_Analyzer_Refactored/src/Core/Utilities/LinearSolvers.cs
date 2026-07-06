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

public sealed class SparsePrototypeLinearSystemSolver : ILinearSystemSolver
{
    private readonly ILinearSystemSolver _fallbackSolver;

    public SparsePrototypeLinearSystemSolver(ILinearSystemSolver? fallbackSolver = null)
    {
        _fallbackSolver = fallbackSolver ?? new DenseLinearSystemSolver();
    }

    public string Name => $"Sparse prototype adapter ({_fallbackSolver.Name} fallback)";

    public double[] Solve(double[,] matrix, double[] rhs)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(rhs);

        var sparse = SparseMatrix.FromDense(matrix);
        return _fallbackSolver.Solve(sparse.ToDense(), rhs);
    }
}
