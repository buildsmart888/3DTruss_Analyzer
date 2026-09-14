namespace TrussAnalyzer.Core.Application;

using TrussAnalyzer.Core.Models;

public sealed record ResultExplorerMember(Guid MemberId, double MaxAxial, double MaxShearY, double MaxShearZ,
    double MaxTorsion, double MaxMomentY, double MaxMomentZ, Guid GoverningSnapshotId);

public sealed record ResultExplorerEnvelope(IReadOnlyList<AnalysisSnapshot> Snapshots,
    IReadOnlyList<ResultExplorerMember> Members, double MaxDisplacement, double MaxUtilization)
{
    public IReadOnlyList<ResultExplorerStationEnvelope> StationEnvelopes { get; init; } = Array.Empty<ResultExplorerStationEnvelope>();
}

public enum ResultComponent { N, Vy, Vz, T, My, Mz }

public sealed record SignedEnvelopeValue(double Minimum, Guid MinimumSnapshotId, Guid MinimumSelectionId,
    double Maximum, Guid MaximumSnapshotId, Guid MaximumSelectionId);

public sealed record ResultExplorerStationEnvelope(Guid MemberId, double RelativePosition,
    DiagramStationSide Side, IReadOnlyDictionary<ResultComponent, SignedEnvelopeValue> Components);

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
        var stationEnvelopes = selected
            .SelectMany(snapshot => snapshot.Members.SelectMany(member => member.Result.StationResults.Select(station => (snapshot, member, station))))
            .GroupBy(value => (value.member.LineObjectId, value.station.RelativePosition, value.station.DiagramSide))
            .Select(group => new ResultExplorerStationEnvelope(group.Key.LineObjectId, group.Key.RelativePosition, group.Key.DiagramSide,
                Enum.GetValues<ResultComponent>().ToDictionary(component => component, component => BuildSignedEnvelope(group, component))))
            .OrderBy(value => value.MemberId).ThenBy(value => value.RelativePosition).ThenBy(value => value.Side)
            .ToArray();
        return new(selected, members, selected.Max(snapshot => snapshot.MaxDisplacement), selected.Max(snapshot => snapshot.MaxUtilization))
        {
            StationEnvelopes = stationEnvelopes
        };
    }

    private static SignedEnvelopeValue BuildSignedEnvelope(
        IEnumerable<(AnalysisSnapshot snapshot, AnalysisSnapshotMember member, ElementStationResult station)> source,
        ResultComponent component)
    {
        var values = source.Select(value => (value.snapshot, Value(value.station, component))).ToArray();
        var minimum = values.OrderBy(value => value.Item2).ThenBy(value => value.snapshot.SnapshotId).First();
        var maximum = values.OrderByDescending(value => value.Item2).ThenBy(value => value.snapshot.SnapshotId).First();
        return new SignedEnvelopeValue(minimum.Item2, minimum.snapshot.SnapshotId, minimum.snapshot.SelectionId,
            maximum.Item2, maximum.snapshot.SnapshotId, maximum.snapshot.SelectionId);
    }

    private static double Value(ElementStationResult station, ResultComponent component) => component switch
    {
        ResultComponent.N => station.AxialForce,
        ResultComponent.Vy => station.ShearY,
        ResultComponent.Vz => station.ShearZ,
        ResultComponent.T => station.Torsion,
        ResultComponent.My => station.MomentY,
        ResultComponent.Mz => station.MomentZ,
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, null)
    };

    private static double Max(ElementForceResult result) => new[] { Math.Abs(result.AxialForce), Math.Abs(result.ShearY), Math.Abs(result.ShearZ), Math.Abs(result.Torsion), Math.Abs(result.MomentY), Math.Abs(result.MomentZ), Math.Abs(result.Stress) }.Max();
}
