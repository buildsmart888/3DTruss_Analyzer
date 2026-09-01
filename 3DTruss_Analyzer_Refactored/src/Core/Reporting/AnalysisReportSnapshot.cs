namespace TrussAnalyzer.Core.Reporting;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;

public sealed class AnalysisReportSnapshot
{
    public string Title { get; init; } = $"{ProductIdentity.Name} Analysis Report";
    public string LoadCaseName { get; init; } = "Default";
    public int NodeCount { get; init; }
    public int ElementCount { get; init; }
    public double MaxDisplacement { get; init; }
    public double MaxAxialForce { get; init; }
    public double MaxStress { get; init; }
    public EquilibriumCheck Equilibrium { get; init; } = new(0, 0, 0, 1e-6);
    public IReadOnlyList<ReportMemberForceRow> MemberForceEnvelope { get; init; } = Array.Empty<ReportMemberForceRow>();
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();

    public static AnalysisReportSnapshot FromAnalysisResult(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new AnalysisReportSnapshot
        {
            LoadCaseName = result.LoadCaseName,
            NodeCount = result.Nodes.Count,
            ElementCount = result.Elements.Count,
            MaxDisplacement = result.MaxDisplacement,
            MaxAxialForce = result.MaxAxialForce,
            MaxStress = result.MaxStress,
            Equilibrium = result.Equilibrium,
            MemberForceEnvelope = result.Elements
                .OrderBy(e => e.Id)
                .Select(e => new ReportMemberForceRow
                {
                    ElementId = e.Id,
                    AxialForce = e.AxialForce,
                    Stress = e.Stress,
                    Utilization = result.SafetyChecks.ElementChecks.FirstOrDefault(c => c.ElementId == e.Id)?.UtilizationRatio ?? 0
                })
                .ToList(),
            Limitations = new[]
            {
                "Internal analysis units: m, N, N-m, Pa.",
                "Linear elastic small-displacement analysis only.",
                "Design checks are preliminary MVP checks and are not final professional design output."
            }
        };
    }
}

public sealed class ReportMemberForceRow
{
    public int ElementId { get; init; }
    public double AxialForce { get; init; }
    public double Stress { get; init; }
    public double Utilization { get; init; }
}
