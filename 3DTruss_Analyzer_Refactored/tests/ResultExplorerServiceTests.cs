namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Application;
using TrussAnalyzer.Core.Models;
using Xunit;

public sealed class ResultExplorerServiceTests
{
    [Fact]
    public void FilterAndEnvelope_SelectGoverningSnapshotDeterministically()
    {
        var memberId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var first = Snapshot(memberId, 10, 2, ProjectAnalysisSelectionKind.LoadPattern);
        var second = Snapshot(memberId, 25, 4, ProjectAnalysisSelectionKind.LoadCombination);
        var explorer = new ResultExplorerService();

        Assert.Single(explorer.Filter(new[] { first, second }, ProjectAnalysisSelectionKind.LoadPattern));
        var envelope = explorer.BuildEnvelope(new[] { first, second });
        Assert.Equal(25, envelope.Members.Single().MaxAxial);
        Assert.Equal(second.SnapshotId, envelope.Members.Single().GoverningSnapshotId);
        Assert.Equal(4, envelope.MaxUtilization);
    }

    private static AnalysisSnapshot Snapshot(Guid memberId, double axial, double utilization, ProjectAnalysisSelectionKind kind) => new()
    {
        SelectionKind = kind, SelectionId = Guid.NewGuid(), MaxDisplacement = axial / 100,
        MaxUtilization = utilization, Members = new[] { new AnalysisSnapshotMember(memberId, new ElementForceResult { AxialForce = axial }) }
    };
}
