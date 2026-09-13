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

    [Fact]
    public void GeneratedLoadRequiresExplicitManualOverwriteConfirmation()
    {
        var service = new LoadWorkspaceService();
        var node = Guid.NewGuid(); var pattern = Guid.NewGuid(); var manual = Guid.NewGuid();
        var document = new ProjectDocument { Model = new Model3D { Nodes = new() { new Node3D { Id = node } } }, LoadDefinitions = new LoadDefinitions { LoadPatterns = new() { new LoadPattern3D { Id = pattern, Label = "DL" } }, Assignments = new() { new NodalLoadAssignment3D { Id = manual, Label = "Manual", LoadPatternId = pattern, NodeId = node, Force = new(1, 0, 0), Source = new SourceMetadata { SourceSystem = "UI" } } } } };
        var generated = new NodalLoadAssignment3D { Id = Guid.NewGuid(), Label = "Generated", LoadPatternId = pattern, NodeId = node, Force = new(2, 0, 0) };
        Assert.Single(service.GetOverwriteWarnings(document, generated));
        Assert.Throws<LoadOverwriteRequiredException>(() => service.UpsertGenerated(document, generated, false));
        Assert.Single(service.UpsertGenerated(document, generated, true).LoadDefinitions.Assignments);
    }

    [Fact]
    public void PropertyEditorUpdatesNodalAssignmentThroughService()
    {
        var node = Guid.NewGuid(); var pattern = Guid.NewGuid(); var load = Guid.NewGuid();
        var document = new ProjectDocument { Model = new Model3D { Nodes = new() { new Node3D { Id = node } } }, LoadDefinitions = new LoadDefinitions { LoadPatterns = new() { new LoadPattern3D { Id = pattern } }, Assignments = new() { new NodalLoadAssignment3D { Id = load, LoadPatternId = pattern, NodeId = node } } } };
        document = new LoadWorkspaceService().UpdateAssignment(document, load, editor => { editor.Label = "Edited"; editor.Z = -500; });
        var updated = Assert.IsType<NodalLoadAssignment3D>(Assert.Single(document.LoadDefinitions.Assignments));
        Assert.Equal("Edited", updated.Label); Assert.Equal(-500, updated.Force.Z);
    }

    [Fact]
    public void TrapezoidalLineLoadIntegrationMatchesAverageIntensity()
    {
        var load = new LineLoadAssignment3D { ForcePerLength = new(0, 0, -2), EndForcePerLength = new(0, 0, -6) };
        var resultant = LoadWorkspaceService.IntegrateLineLoad(load, 5);
        Assert.Equal(-20, resultant.Z, precision: 12);
    }
}
