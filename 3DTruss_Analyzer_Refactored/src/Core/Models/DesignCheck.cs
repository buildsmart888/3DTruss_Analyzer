namespace TrussAnalyzer.Core.Models;

/// <summary>
/// Safety check result for one truss element.
/// </summary>
public class ElementSafetyCheck
{
    public int ElementId { get; init; }
    public double DemandStress { get; init; }
    public double AllowableStress { get; init; }
    public double UtilizationRatio { get; init; }
    public bool IsPassing { get; init; }
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// Overall safety check summary for an analysis result.
/// </summary>
public class SafetyCheckSummary
{
    public List<ElementSafetyCheck> ElementChecks { get; init; } = new();
    public bool AllKnownChecksPass => ElementChecks.All(c => c.IsPassing);
    public double MaxUtilizationRatio => ElementChecks.Count == 0 ? 0 : ElementChecks.Max(c => c.UtilizationRatio);
}

/// <summary>
/// A model validation message intended for UI and diagnostics.
/// </summary>
public class ModelValidationMessage
{
    public string Severity { get; init; } = "Info";
    public string Message { get; init; } = string.Empty;
    public SelectedModelObjectType ObjectType { get; init; } = SelectedModelObjectType.None;
    public int ObjectId { get; init; }
}
