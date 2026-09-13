namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Application;
using TrussAnalyzer.Core.Domain.V1;
using Xunit;

public sealed class LoadWorkspaceServiceTests
{
    [Fact]
    public void AuthoringPatternsAssignmentsAndLedgerRemainTraceable()
    {
        var nodeId = Guid.NewGuid(); var patternId = Guid.NewGuid(); var loadId = Guid.NewGuid();
        var document = new ProjectDocument { Model = new Model3D { Nodes = new() { new Node3D { Id = nodeId, Position = new(0, 0, 0) } } } };
        var service = new LoadWorkspaceService();
        document = service.EnsurePattern(document, "DL", LoadPatternKind.Dead, 1, patternId);
        document = service.UpsertNodal(document, new NodalLoadAssignment3D { Id = loadId, Label = "Roof reaction", LoadPatternId = patternId, NodeId = nodeId, Force = new(0, 0, -1000), Source = new SourceMetadata { SourceSystem = "UI", SourceObjectId = "roof-1" } });

        var entry = Assert.Single(service.BuildLedger(document));
        Assert.Equal("DL", entry.Pattern); Assert.Equal("N / N-m", entry.Units); Assert.Equal("UI", entry.Source); Assert.Empty(service.Validate(document));
    }

    [Fact]
    public void LineLoadRejectsInvalidRangeAndMissingReferences()
    {
        var service = new LoadWorkspaceService(); var document = new ProjectDocument();
        var patternId = Guid.NewGuid(); document = service.EnsurePattern(document, "LL", LoadPatternKind.Live, id: patternId);
        var assignment = new LineLoadAssignment3D { LoadPatternId = patternId, LineObjectId = Guid.NewGuid(), StartRelativePosition = .8, EndRelativePosition = .2 };
        Assert.Throws<InvalidOperationException>(() => service.UpsertLine(document, assignment));
    }
}
