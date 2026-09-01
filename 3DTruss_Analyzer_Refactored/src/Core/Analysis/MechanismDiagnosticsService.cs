namespace TrussAnalyzer.Core.Analysis;

using TrussAnalyzer.Core.Models;

public sealed record MechanismDofDiagnostic(int GlobalDof, int NodeId, string DegreeOfFreedom, string Reason);

public sealed class MechanismDiagnosticReport
{
    public int? FailedPivotDof { get; init; }
    public IReadOnlyList<MechanismDofDiagnostic> SuspectDofs { get; init; } = Array.Empty<MechanismDofDiagnostic>();

    public string ToUserMessage()
    {
        if (SuspectDofs.Count == 0)
            return "Matrix is singular or nearly singular. Check support restraints, member connectivity, and frame releases.";

        string locations = string.Join(", ", SuspectDofs.Take(6).Select(diagnostic => $"Node {diagnostic.NodeId} {diagnostic.DegreeOfFreedom}"));
        string suffix = SuspectDofs.Count > 6 ? ", ..." : string.Empty;
        return $"Matrix is singular or nearly singular. Suspect DOF(s): {locations}{suffix}. Check support restraints, member connectivity, and frame releases.";
    }
}

public sealed class StructuralInstabilityException : InvalidOperationException
{
    public StructuralInstabilityException(MechanismDiagnosticReport diagnostics, Exception innerException)
        : base(diagnostics.ToUserMessage(), innerException)
    {
        Diagnostics = diagnostics;
    }

    public MechanismDiagnosticReport Diagnostics { get; }
}

public sealed class MechanismDiagnosticsService
{
    private const double PivotTolerance = 1e-12;
    private static readonly string[] DofNames = { "UX", "UY", "UZ", "RX", "RY", "RZ" };

    public MechanismDiagnosticReport Analyze(double[,] constrainedStiffness, IReadOnlyList<Node> nodes)
    {
        ArgumentNullException.ThrowIfNull(constrainedStiffness);
        ArgumentNullException.ThrowIfNull(nodes);

        int size = constrainedStiffness.GetLength(0);
        if (constrainedStiffness.GetLength(1) != size || size != nodes.Count * DofIndexer.DofPerNode)
            throw new ArgumentException("Constrained stiffness matrix dimensions must match the model node DOF count.", nameof(constrainedStiffness));

        var suspectDofs = FindZeroStiffnessRows(constrainedStiffness, nodes);
        int? failedPivotDof = FindFailedPivotDof(constrainedStiffness);
        if (failedPivotDof.HasValue && suspectDofs.All(diagnostic => diagnostic.GlobalDof != failedPivotDof.Value))
        {
            suspectDofs.Add(CreateDiagnostic(
                failedPivotDof.Value,
                nodes,
                "Rank-deficient pivot detected; this DOF may participate in a mechanism."));
        }

        return new MechanismDiagnosticReport
        {
            FailedPivotDof = failedPivotDof,
            SuspectDofs = suspectDofs
        };
    }

    public static bool IsSingularOrUnstableFailure(InvalidOperationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.Message.Contains("singular", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("unstable", StringComparison.OrdinalIgnoreCase);
    }

    private static List<MechanismDofDiagnostic> FindZeroStiffnessRows(double[,] matrix, IReadOnlyList<Node> nodes)
    {
        int size = matrix.GetLength(0);
        var diagnostics = new List<MechanismDofDiagnostic>();
        for (int row = 0; row < size; row++)
        {
            double maxMagnitude = 0;
            for (int column = 0; column < size; column++)
                maxMagnitude = Math.Max(maxMagnitude, Math.Abs(matrix[row, column]));

            if (maxMagnitude < PivotTolerance)
            {
                diagnostics.Add(CreateDiagnostic(
                    row,
                    nodes,
                    "No effective stiffness is assembled for this DOF."));
            }
        }

        return diagnostics;
    }

    private static int? FindFailedPivotDof(double[,] matrix)
    {
        int size = matrix.GetLength(0);
        var working = (double[,])matrix.Clone();
        for (int column = 0; column < size; column++)
        {
            int maxRow = column;
            double maxValue = Math.Abs(working[column, column]);
            for (int row = column + 1; row < size; row++)
            {
                double value = Math.Abs(working[row, column]);
                if (value > maxValue)
                {
                    maxValue = value;
                    maxRow = row;
                }
            }

            if (maxValue < PivotTolerance)
                return column;

            if (maxRow != column)
            {
                for (int swapColumn = column; swapColumn < size; swapColumn++)
                    (working[column, swapColumn], working[maxRow, swapColumn]) = (working[maxRow, swapColumn], working[column, swapColumn]);
            }

            for (int row = column + 1; row < size; row++)
            {
                double factor = working[row, column] / working[column, column];
                for (int eliminateColumn = column; eliminateColumn < size; eliminateColumn++)
                    working[row, eliminateColumn] -= factor * working[column, eliminateColumn];
            }
        }

        return null;
    }

    private static MechanismDofDiagnostic CreateDiagnostic(int globalDof, IReadOnlyList<Node> nodes, string reason)
    {
        int nodeIndex = globalDof / DofIndexer.DofPerNode;
        int localDof = globalDof % DofIndexer.DofPerNode;
        return new MechanismDofDiagnostic(globalDof, nodes[nodeIndex].Id, DofNames[localDof], reason);
    }
}
