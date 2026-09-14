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

    [Fact]
    public void Envelope_PreservesSignedExtremaAndGoverningCasePerStationComponentAndSide()
    {
        var memberId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var positiveCase = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var negativeCase = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var positive = StationSnapshot(memberId, positiveCase, 0.5, DiagramStationSide.Left, axial: 12, shearY: -3);
        var negative = StationSnapshot(memberId, negativeCase, 0.5, DiagramStationSide.Left, axial: -20, shearY: 8);
        var right = StationSnapshot(memberId, positiveCase, 0.5, DiagramStationSide.Right, axial: 99, shearY: 50);

        var envelope = new ResultExplorerService().BuildEnvelope(new[] { positive, negative, right });
        var left = Assert.Single(envelope.StationEnvelopes.Where(value => value.Side == DiagramStationSide.Left));
        var axial = left.Components[ResultComponent.N];
        var shear = left.Components[ResultComponent.Vy];

        Assert.Equal(-20, axial.Minimum);
        Assert.Equal(negativeCase, axial.MinimumSelectionId);
        Assert.Equal(12, axial.Maximum);
        Assert.Equal(positiveCase, axial.MaximumSelectionId);
        Assert.Equal(-3, shear.Minimum);
        Assert.Equal(8, shear.Maximum);
        Assert.Contains(envelope.StationEnvelopes, value => value.Side == DiagramStationSide.Right && value.Components[ResultComponent.N].Maximum == 99);
    }

    private static AnalysisSnapshot Snapshot(Guid memberId, double axial, double utilization, ProjectAnalysisSelectionKind kind) => new()
    {
        SelectionKind = kind, SelectionId = Guid.NewGuid(), MaxDisplacement = axial / 100,
        MaxUtilization = utilization, Members = new[] { new AnalysisSnapshotMember(memberId, new ElementForceResult { AxialForce = axial }) }
    };

    private static AnalysisSnapshot StationSnapshot(Guid memberId, Guid selectionId, double position, DiagramStationSide side, double axial, double shearY) => new()
    {
        SelectionKind = ProjectAnalysisSelectionKind.LoadPattern,
        SelectionId = selectionId,
        Members = new[]
        {
            new AnalysisSnapshotMember(memberId, new ElementForceResult
            {
                StationResults = new List<ElementStationResult>
                {
                    new() { ElementId = 1, RelativePosition = position, DiagramSide = side, AxialForce = axial, ShearY = shearY }
                }
            })
        }
    };
}
