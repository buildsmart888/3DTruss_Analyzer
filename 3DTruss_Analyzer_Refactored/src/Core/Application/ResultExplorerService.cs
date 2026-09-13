namespace TrussAnalyzer.Core.Application;

using TrussAnalyzer.Core.Models;

public sealed record ResultExplorerMember(Guid MemberId, double MaxAxial, double MaxShearY, double MaxShearZ,
    double MaxTorsion, double MaxMomentY, double MaxMomentZ, Guid GoverningSnapshotId);

public sealed record ResultExplorerEnvelope(IReadOnlyList<AnalysisSnapshot> Snapshots,
    IReadOnlyList<ResultExplorerMember> Members, double MaxDisplacement, double MaxUtilization);

/// <summary>Provides deterministic case/combo filtering and envelope aggregation for UI and reports.</summary>
public sealed class ResultExplorerService
{
    public IReadOnlyList<AnalysisSnapshot> Filter(IEnumerable<AnalysisSnapshot> snapshots,
        ProjectAnalysisSelectionKind? kind = null, Guid? selectionId = null)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        return snapshots.Where(snapshot => (!kind.HasValue || snapshot.SelectionKind == kind.Value) &&
                                           (!selectionId.HasValue || snapshot.SelectionId == selectionId.Value))
            .OrderBy(snapshot => snapshot.SelectionKind).ThenBy(snapshot => snapshot.SelectionId).ToArray();
    }

    public ResultExplorerEnvelope BuildEnvelope(IEnumerable<AnalysisSnapshot> snapshots)
    {
        var selected = snapshots?.ToArray() ?? throw new ArgumentNullException(nameof(snapshots));
        if (selected.Length == 0) return new(Array.Empty<AnalysisSnapshot>(), Array.Empty<ResultExplorerMember>(), 0, 0);
        var members = selected.SelectMany(snapshot => snapshot.Members.Select(member => (snapshot, member)))
            .GroupBy(value => value.member.LineObjectId)
            .Select(group =>
            {
                var governing = group.OrderByDescending(value => Max(value.member.Result)).First();
                return new ResultExplorerMember(group.Key,
                    group.Max(value => Math.Abs(value.member.Result.AxialForce)),
                    group.Max(value => Math.Abs(value.member.Result.ShearY)),
                    group.Max(value => Math.Abs(value.member.Result.ShearZ)),
                    group.Max(value => Math.Abs(value.member.Result.Torsion)),
                    group.Max(value => Math.Abs(value.member.Result.MomentY)),
                    group.Max(value => Math.Abs(value.member.Result.MomentZ)), governing.snapshot.SnapshotId);
            }).OrderBy(member => member.MemberId).ToArray();
        return new(selected, members, selected.Max(snapshot => snapshot.MaxDisplacement), selected.Max(snapshot => snapshot.MaxUtilization));
    }

    private static double Max(ElementForceResult result) => new[] { Math.Abs(result.AxialForce), Math.Abs(result.ShearY), Math.Abs(result.ShearZ), Math.Abs(result.Torsion), Math.Abs(result.MomentY), Math.Abs(result.MomentZ), Math.Abs(result.Stress) }.Max();
}
