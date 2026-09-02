namespace TrussAnalyzer.Core.Application;

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TrussAnalyzer.Core.Domain.V1;
using TrussAnalyzer.Core.Domain.V1.Adapters;
using TrussAnalyzer.Core.Models;

public enum ProjectAnalysisSelectionKind { LoadPattern, LoadCombination }

public sealed record ProjectAnalysisRequest(ProjectAnalysisSelectionKind Kind, Guid SelectionId);

public sealed record AnalysisPreflightMessage(string Code, string Severity, string Message, Guid? ObjectId = null);

public sealed record AnalysisSnapshotNode(Guid NodeId, Vector3D Displacement, Vector3D Rotation, Vector3D ReactionForce, Vector3D ReactionMoment);
public sealed record AnalysisSnapshotMember(Guid LineObjectId, ElementForceResult Result);

/// <summary>Immutable result envelope required by ENG-005; it never mutates the ProjectDocument.</summary>
public sealed record AnalysisSnapshot
{
    public Guid SnapshotId { get; init; } = Guid.NewGuid();
    public string DocumentChecksum { get; init; } = string.Empty;
    public ProjectAnalysisSelectionKind SelectionKind { get; init; }
    public Guid SelectionId { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public string SolverName { get; init; } = string.Empty;
    public string SolverVersion { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<AnalysisSnapshotNode> Nodes { get; init; } = Array.Empty<AnalysisSnapshotNode>();
    public IReadOnlyList<AnalysisSnapshotMember> Members { get; init; } = Array.Empty<AnalysisSnapshotMember>();
    public EquilibriumCheck Equilibrium { get; init; } = new(0, 0, 0, 1e-6);
}

public sealed record ProjectAnalysisResult(IReadOnlyList<AnalysisPreflightMessage> Preflight, AnalysisSnapshot? Snapshot)
{
    public bool Succeeded => Snapshot is not null && Preflight.All(message => !string.Equals(message.Severity, "Error", StringComparison.Ordinal));
}

/// <summary>
/// The only application-facing route from ProjectDocument to the native solver. It enforces a loss-aware
/// preflight and replaces legacy integer result IDs with Model3D GUID identities.
/// </summary>
public sealed class ProjectAnalysisService
{
    private readonly StructuralModelModel3DAdapter _adapter;

    public ProjectAnalysisService(StructuralModelModel3DAdapter? adapter = null) => _adapter = adapter ?? new StructuralModelModel3DAdapter();

    public ProjectAnalysisResult Analyze(ProjectDocument document, ProjectAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(document);
        var preflight = ValidateRequest(document, request).ToList();
        var validation = new Model3DValidator().Validate(document);
        preflight.AddRange(validation.Select(issue => new AnalysisPreflightMessage(
            issue.Code.ToString(), issue.Code == ValidationCode.UnsupportedAnalysisBehavior ? "Error" : issue.Severity.ToString(), issue.Message, issue.ObjectId)));
        if (HasErrors(preflight)) return new ProjectAnalysisResult(preflight, null);

        var adapted = _adapter.ToStructuralModel(document);
        preflight.AddRange(adapted.Diagnostics.Select(diagnostic => new AnalysisPreflightMessage(
            diagnostic.Code,
            diagnostic.Severity == AdapterDiagnosticSeverity.Error ? "Error" : "Warning",
            diagnostic.Message)));
        // A lossy adapter diagnostic is never allowed to become plausible-looking production output.
        if (adapted.Diagnostics.Any(IsLossyAdapterDiagnostic))
        {
            preflight.Add(new AnalysisPreflightMessage("ANALYSIS-LOSSY-ADAPTER", "Error",
                "The selected ProjectDocument cannot be analyzed through StructuralModel without loss. Resolve the listed adapter diagnostics first."));
            return new ProjectAnalysisResult(preflight, null);
        }

        try
        {
            var solver = new StructuralSolver(adapted.Model);
            StructuralAnalysisResult result = request.Kind switch
            {
                ProjectAnalysisSelectionKind.LoadPattern => solver.Analyze(ResolveLoadCaseId(document, request.SelectionId)),
                ProjectAnalysisSelectionKind.LoadCombination => solver.AnalyzeCombination(request.SelectionId.ToString("N")),
                _ => throw new InvalidOperationException($"Unsupported selection kind '{request.Kind}'.")
            };
            var snapshot = BuildSnapshot(document, request, adapted, result, preflight);
            return new ProjectAnalysisResult(preflight, snapshot);
        }
        catch (InvalidOperationException ex)
        {
            preflight.Add(new AnalysisPreflightMessage("ANALYSIS-SOLVER", "Error", ex.Message));
            return new ProjectAnalysisResult(preflight, null);
        }
    }

    private static IEnumerable<AnalysisPreflightMessage> ValidateRequest(ProjectDocument document, ProjectAnalysisRequest request)
    {
        bool valid = request.Kind switch
        {
            ProjectAnalysisSelectionKind.LoadPattern => document.LoadDefinitions.LoadPatterns.Any(pattern => pattern.Id == request.SelectionId),
            ProjectAnalysisSelectionKind.LoadCombination => document.LoadDefinitions.LoadCombinations.Any(combination => combination.Id == request.SelectionId),
            _ => false
        };
        if (!valid)
            yield return new AnalysisPreflightMessage("ANALYSIS-SELECTION", "Error", $"The requested {request.Kind} ID '{request.SelectionId}' does not exist.", request.SelectionId);
        if (request.Kind == ProjectAnalysisSelectionKind.LoadPattern && valid)
        {
            var pattern = document.LoadDefinitions.LoadPatterns.Single(value => value.Id == request.SelectionId);
            if (string.IsNullOrWhiteSpace(pattern.Source.SourceObjectId))
                yield return new AnalysisPreflightMessage("ANALYSIS-PATTERN-MAPPING", "Error",
                    $"Load pattern '{pattern.Label}' has no stable StructuralModel case ID. Import it through an explicit adapter or define a supported case mapping.", pattern.Id);
        }
    }

    private static string ResolveLoadCaseId(ProjectDocument document, Guid patternId) =>
        document.LoadDefinitions.LoadPatterns.Single(pattern => pattern.Id == patternId).Source.SourceObjectId;

    private static AnalysisSnapshot BuildSnapshot(ProjectDocument document, ProjectAnalysisRequest request,
        ProjectDocumentToStructuralModelResult adapted, StructuralAnalysisResult result, IReadOnlyList<AnalysisPreflightMessage> preflight)
    {
        var nodes = adapted.NodeIds.ToDictionary(pair => pair.Value, pair => pair.Key);
        var members = adapted.LineObjectIds.ToDictionary(pair => pair.Value, pair => pair.Key);
        return new AnalysisSnapshot
        {
            DocumentChecksum = ComputeChecksum(document),
            SelectionKind = request.Kind,
            SelectionId = request.SelectionId,
            CreatedUtc = DateTimeOffset.UtcNow,
            SolverName = result.Diagnostics.SolverName,
            SolverVersion = Assembly.GetAssembly(typeof(StructuralSolver))?.GetName().Version?.ToString() ?? "unknown",
            Warnings = preflight.Where(message => string.Equals(message.Severity, "Warning", StringComparison.Ordinal)).Select(message => $"{message.Code}: {message.Message}").ToArray(),
            Nodes = result.NodeResults.Select(node => new AnalysisSnapshotNode(nodes[node.NodeId], node.Displacement, node.Rotation, node.ReactionForce, node.ReactionMoment)).ToArray(),
            Members = result.ElementResults.Select(member => new AnalysisSnapshotMember(members[member.ElementId], member)).ToArray(),
            Equilibrium = result.Equilibrium
        };
    }

    private static bool HasErrors(IEnumerable<AnalysisPreflightMessage> messages) => messages.Any(message => string.Equals(message.Severity, "Error", StringComparison.Ordinal));
    private static bool IsLossyAdapterDiagnostic(AdapterDiagnostic diagnostic) =>
        diagnostic.Severity == AdapterDiagnosticSeverity.Error ||
        // Legacy-ID allocation changes no engineering value; it is retained as a visible snapshot warning.
        (diagnostic.Severity == AdapterDiagnosticSeverity.Warning && diagnostic.Code != "M32SM-NEW-LEGACY-ID");
    private static string ComputeChecksum(ProjectDocument document) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectDocumentJson.Serialize(document))));
}
