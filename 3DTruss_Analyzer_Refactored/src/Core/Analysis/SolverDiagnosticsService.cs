namespace TrussAnalyzer.Core.Analysis;

using TrussAnalyzer.Core.Models;

public sealed class SolverDiagnosticsService
{
    private readonly int _denseSolverWarningDof;

    public SolverDiagnosticsService(int denseSolverWarningDof = 300)
    {
        _denseSolverWarningDof = denseSolverWarningDof;
    }

    public SolverDiagnostics Build(
        DofIndexer dofIndexer,
        int elementCount,
        int nonZeroStiffnessEntries,
        string solverName,
        double[] originalLoadVector,
        IReadOnlyList<StructuralNodeResult> nodeResults,
        EquilibriumCheck equilibrium)
    {
        ArgumentNullException.ThrowIfNull(dofIndexer);
        ArgumentNullException.ThrowIfNull(solverName);
        ArgumentNullException.ThrowIfNull(originalLoadVector);
        ArgumentNullException.ThrowIfNull(nodeResults);
        ArgumentNullException.ThrowIfNull(equilibrium);

        int totalDof = dofIndexer.TotalDof;
        double matrixDensity = totalDof == 0 ? 0 : (double)nonZeroStiffnessEntries / (totalDof * totalDof);
        double appliedLoadMagnitude = originalLoadVector.Sum(Math.Abs);
        double reactionMagnitude = nodeResults.Sum(result => result.ReactionForce.Magnitude + result.ReactionMoment.Magnitude);
        bool denseSolverWarning = totalDof > _denseSolverWarningDof;

        return new SolverDiagnostics
        {
            TotalDof = totalDof,
            ConstrainedDof = dofIndexer.ConstrainedDof,
            ElementCount = elementCount,
            SolverName = solverName,
            DenseSolverWarning = denseSolverWarning,
            MatrixDensity = matrixDensity,
            AppliedLoadMagnitude = appliedLoadMagnitude,
            ReactionMagnitude = reactionMagnitude,
            EquilibriumResidualMagnitude = equilibrium.ResidualMagnitude,
            Notes = denseSolverWarning
                ? "Dense solver path is active; use sparse solver for larger production models."
                : "Dense solver path is active."
        };
    }
}
