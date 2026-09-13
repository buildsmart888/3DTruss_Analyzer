namespace TrussAnalyzer.Core.Application;

using TrussAnalyzer.Core.Domain.V1;

/// <summary>Application boundary for traceable load authoring. UI code does not construct load contracts directly.</summary>
public sealed class LoadWorkspaceService
{
    public ProjectDocument EnsurePattern(ProjectDocument document, string label, LoadPatternKind kind, double selfWeightMultiplier = 0, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A load pattern label is required.", nameof(label));
        if (!double.IsFinite(selfWeightMultiplier)) throw new ArgumentOutOfRangeException(nameof(selfWeightMultiplier));
        if (document.LoadDefinitions.LoadPatterns.Any(pattern => string.Equals(pattern.Label, label, StringComparison.OrdinalIgnoreCase))) return document;
        var patternId = id ?? Guid.NewGuid();
        var pattern = new LoadPattern3D { Id = patternId, Label = label.Trim(), Kind = kind, SelfWeightMultiplier = selfWeightMultiplier,
            Source = new SourceMetadata { SourceSystem = "LoadWorkspace", SourceVersion = "F1", SourceObjectId = patternId.ToString("N") } };
        return Touch(document with { LoadDefinitions = document.LoadDefinitions with { LoadPatterns = document.LoadDefinitions.LoadPatterns.Append(pattern).ToList() } });
    }

    public ProjectDocument UpsertNodal(ProjectDocument document, NodalLoadAssignment3D assignment)
    {
        RequirePattern(document, assignment.LoadPatternId); RequireNode(document, assignment.NodeId);
        var assignments = document.LoadDefinitions.Assignments.Where(item => item.Id != assignment.Id).Append(assignment).ToList();
        return Touch(document with { LoadDefinitions = document.LoadDefinitions with { Assignments = assignments } });
    }

    public IReadOnlyList<LoadOverwriteWarning> GetOverwriteWarnings(ProjectDocument document, LoadAssignment3D generated)
    {
        return document.LoadDefinitions.Assignments
            .Where(existing => existing.Id != generated.Id && existing.LoadPatternId == generated.LoadPatternId && SameTarget(existing, generated) && !IsGenerated(existing))
            .Select(existing => new LoadOverwriteWarning(existing.Id, existing.Label, generated.Label, "A manual load targets the same object and pattern."))
            .ToList();
    }

    public ProjectDocument UpsertGenerated(ProjectDocument document, LoadAssignment3D generated, bool overwriteManual)
    {
        var warnings = GetOverwriteWarnings(document, generated);
        if (warnings.Count > 0 && !overwriteManual) throw new LoadOverwriteRequiredException(warnings);
        var source = generated.Source with { SourceSystem = string.IsNullOrWhiteSpace(generated.Source.SourceSystem) ? "Generated" : generated.Source.SourceSystem, Notes = "Generated assignment; regeneration may replace this record." };
        var updated = generated switch
        {
            NodalLoadAssignment3D value => UpsertNodal(document, value with { Source = source }),
            LineLoadAssignment3D value => UpsertLine(document, value with { Source = source }),
            _ => throw new InvalidOperationException("Generated assignment type is not supported by this workflow.")
        };
        if (warnings.Count == 0) return updated;
        var ids = warnings.Select(warning => warning.ExistingId).ToHashSet();
        return updated with { LoadDefinitions = updated.LoadDefinitions with { Assignments = updated.LoadDefinitions.Assignments.Where(item => !ids.Contains(item.Id)).ToList() } };
    }

    public ProjectDocument UpsertLine(ProjectDocument document, LineLoadAssignment3D assignment)
    {
        RequirePattern(document, assignment.LoadPatternId); RequireLine(document, assignment.LineObjectId);
        if (assignment.StartRelativePosition < 0 || assignment.EndRelativePosition > 1 || assignment.EndRelativePosition < assignment.StartRelativePosition)
            throw new ArgumentOutOfRangeException(nameof(assignment), "Line load relative positions must be within [0,1].");
        var assignments = document.LoadDefinitions.Assignments.Where(item => item.Id != assignment.Id).Append(assignment).ToList();
        return Touch(document with { LoadDefinitions = document.LoadDefinitions with { Assignments = assignments } });
    }

    public ProjectDocument UpdateAssignment(ProjectDocument document, Guid assignmentId, Action<LoadAssignmentEditor> edit)
    {
        var existing = document.LoadDefinitions.Assignments.SingleOrDefault(item => item.Id == assignmentId) ?? throw new InvalidOperationException("Load assignment was not found.");
        var editor = LoadAssignmentEditor.From(existing); edit(editor);
        return editor.Build(existing) switch
        {
            NodalLoadAssignment3D nodal => UpsertNodal(document, nodal),
            LineLoadAssignment3D line => UpsertLine(document, line),
            _ => throw new InvalidOperationException("This assignment type is read-only in the current property editor.")
        };
    }

    public ProjectDocument UpsertCombination(ProjectDocument document, LoadCombination3D combination)
    {
        var patternIds = document.LoadDefinitions.LoadPatterns.Select(pattern => pattern.Id).ToHashSet();
        if (combination.LoadPatternFactors.Keys.Any(id => !patternIds.Contains(id))) throw new InvalidOperationException("Combination references an unknown load pattern.");
        var combinations = document.LoadDefinitions.LoadCombinations.Where(item => item.Id != combination.Id).Append(combination).ToList();
        return Touch(document with { LoadDefinitions = document.LoadDefinitions with { LoadCombinations = combinations } });
    }

    public IReadOnlyList<LoadAssignmentLedgerEntry> BuildLedger(ProjectDocument document, Guid? patternId = null)
    {
        var patterns = document.LoadDefinitions.LoadPatterns.ToDictionary(pattern => pattern.Id);
        return document.LoadDefinitions.Assignments
            .Where(assignment => patternId is null || assignment.LoadPatternId == patternId)
            .Select(assignment => assignment switch
            {
                NodalLoadAssignment3D nodal => new LoadAssignmentLedgerEntry(assignment.Id, assignment.Label, patterns[assignment.LoadPatternId].Label, "Node", nodal.NodeId, $"F=({nodal.Force.X:G6},{nodal.Force.Y:G6},{nodal.Force.Z:G6}) N; M=({nodal.Moment.X:G6},{nodal.Moment.Y:G6},{nodal.Moment.Z:G6}) N-m", assignment.Source.SourceSystem, "N / N-m"),
                LineLoadAssignment3D line => new LoadAssignmentLedgerEntry(assignment.Id, assignment.Label, patterns[assignment.LoadPatternId].Label, "Member", line.LineObjectId, $"w=({line.ForcePerLength.X:G6},{line.ForcePerLength.Y:G6},{line.ForcePerLength.Z:G6}) N/m; r={line.StartRelativePosition:G4}..{line.EndRelativePosition:G4}", assignment.Source.SourceSystem, "N/m"),
                LinePointLoadAssignment3D point => new LoadAssignmentLedgerEntry(assignment.Id, assignment.Label, patterns[assignment.LoadPatternId].Label, "Member point", point.LineObjectId, $"r={point.RelativePosition:G4}; F=({point.Force.X:G6},{point.Force.Y:G6},{point.Force.Z:G6}) N", assignment.Source.SourceSystem, "N / N-m"),
                TemperatureLoadAssignment3D temperature => new LoadAssignmentLedgerEntry(assignment.Id, assignment.Label, patterns[assignment.LoadPatternId].Label, "Member temperature", temperature.LineObjectId, $"ΔT={temperature.TemperatureChange:G6} K", assignment.Source.SourceSystem, "K"),
                PrescribedMovementAssignment3D movement => new LoadAssignmentLedgerEntry(assignment.Id, assignment.Label, patterns[assignment.LoadPatternId].Label, "Prescribed node", movement.NodeId, $"U=({movement.Movement.UX:G6},{movement.Movement.UY:G6},{movement.Movement.UZ:G6}) m", assignment.Source.SourceSystem, "m / rad"),
                _ => throw new InvalidOperationException($"Unsupported load assignment {assignment.GetType().Name}.")
            }).ToList();
    }

    public IReadOnlyList<string> Validate(ProjectDocument document)
    {
        var issues = new List<string>();
        var patternIds = document.LoadDefinitions.LoadPatterns.Select(pattern => pattern.Id).ToHashSet();
        foreach (var assignment in document.LoadDefinitions.Assignments)
        {
            if (!patternIds.Contains(assignment.LoadPatternId)) issues.Add($"{assignment.Label}: load pattern is missing.");
            if (assignment is NodalLoadAssignment3D node && !document.Model.Nodes.Any(item => item.Id == node.NodeId)) issues.Add($"{assignment.Label}: node is missing.");
            if (assignment is LineLoadAssignment3D line && !document.Model.LineObjects.Any(item => item.Id == line.LineObjectId)) issues.Add($"{assignment.Label}: member is missing.");
            if (assignment is LinePointLoadAssignment3D point && (!document.Model.LineObjects.Any(item => item.Id == point.LineObjectId) || point.RelativePosition is < 0 or > 1)) issues.Add($"{assignment.Label}: point-load member or position is invalid.");
            if (assignment is TemperatureLoadAssignment3D temperature && !document.Model.LineObjects.Any(item => item.Id == temperature.LineObjectId)) issues.Add($"{assignment.Label}: temperature member is missing.");
            if (assignment is PrescribedMovementAssignment3D movement && !document.Model.Nodes.Any(item => item.Id == movement.NodeId)) issues.Add($"{assignment.Label}: prescribed-movement node is missing.");
        }
        return issues;
    }

    private static void RequirePattern(ProjectDocument document, Guid id) { if (!document.LoadDefinitions.LoadPatterns.Any(pattern => pattern.Id == id)) throw new InvalidOperationException("Load pattern was not found."); }
    private static void RequireNode(ProjectDocument document, Guid id) { if (!document.Model.Nodes.Any(node => node.Id == id)) throw new InvalidOperationException("Load node was not found."); }
    private static void RequireLine(ProjectDocument document, Guid id) { if (!document.Model.LineObjects.Any(line => line.Id == id)) throw new InvalidOperationException("Load member was not found."); }
    private static bool SameTarget(LoadAssignment3D left, LoadAssignment3D right) => (left, right) switch { (NodalLoadAssignment3D a, NodalLoadAssignment3D b) => a.NodeId == b.NodeId, (LineLoadAssignment3D a, LineLoadAssignment3D b) => a.LineObjectId == b.LineObjectId, (LinePointLoadAssignment3D a, LinePointLoadAssignment3D b) => a.LineObjectId == b.LineObjectId && Math.Abs(a.RelativePosition - b.RelativePosition) < 1e-9, _ => false };
    private static bool IsGenerated(LoadAssignment3D assignment) => assignment.Source.SourceSystem.Contains("Generated", StringComparison.OrdinalIgnoreCase) || assignment.Source.SourceSystem.Contains("Floor", StringComparison.OrdinalIgnoreCase);
    private static ProjectDocument Touch(ProjectDocument document) => document with { AuditMetadata = document.AuditMetadata with { ModifiedUtc = DateTimeOffset.UtcNow } };
}

public sealed record LoadAssignmentLedgerEntry(Guid Id, string Label, string Pattern, string TargetKind, Guid TargetId, string Value, string Source, string Units);
public sealed record LoadOverwriteWarning(Guid ExistingId, string ExistingLabel, string GeneratedLabel, string Message);
public sealed class LoadOverwriteRequiredException : InvalidOperationException
{
    public IReadOnlyList<LoadOverwriteWarning> Warnings { get; }
    public LoadOverwriteRequiredException(IReadOnlyList<LoadOverwriteWarning> warnings) : base("Generated load would overwrite manual assignments; explicit confirmation is required.") => Warnings = warnings;
}

public sealed class LoadAssignmentEditor
{
    public string Label { get; set; } = string.Empty; public double X { get; set; } public double Y { get; set; } public double Z { get; set; } public double MX { get; set; } public double MY { get; set; } public double MZ { get; set; } public double Start { get; set; } public double End { get; set; } = 1;

    public static LoadAssignmentEditor From(LoadAssignment3D assignment) => assignment switch
    {
        NodalLoadAssignment3D nodal => new() { Label = nodal.Label, X = nodal.Force.X, Y = nodal.Force.Y, Z = nodal.Force.Z, MX = nodal.Moment.X, MY = nodal.Moment.Y, MZ = nodal.Moment.Z },
        LineLoadAssignment3D line => new() { Label = line.Label, X = line.ForcePerLength.X, Y = line.ForcePerLength.Y, Z = line.ForcePerLength.Z, Start = line.StartRelativePosition, End = line.EndRelativePosition },
        _ => throw new InvalidOperationException("Assignment type is not editable by this property editor.")
    };
    internal LoadAssignment3D Build(LoadAssignment3D source) => source switch
    {
        NodalLoadAssignment3D nodal => nodal with { Label = Label, Force = new(X, Y, Z), Moment = new(MX, MY, MZ) },
        LineLoadAssignment3D line => line with { Label = Label, ForcePerLength = new(X, Y, Z), StartRelativePosition = Start, EndRelativePosition = End },
        _ => throw new InvalidOperationException("Assignment type is not editable by this property editor.")
    };
}
