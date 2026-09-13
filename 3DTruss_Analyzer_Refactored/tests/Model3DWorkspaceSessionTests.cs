namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Application;
using TrussAnalyzer.Core.Domain.V1;
using TrussAnalyzer.Core.IO.Projects;
using Xunit;

public sealed class Model3DWorkspaceSessionTests
{
    [Fact]
    public void BrowserAndSelectionUseOneModel3DIdentitySet()
    {
        var nodeId = Guid.NewGuid();
        var levelId = Guid.NewGuid();
        var patternId = Guid.NewGuid();
        var document = new ProjectDocument
        {
            Model = new Model3D
            {
                Levels = new() { new Level3D { Id = levelId, Label = "Level 1", Elevation = 3 } },
                Nodes = new() { new Node3D { Id = nodeId, Label = "N1", Position = new(0, 0, 3) } }
            },
            LoadDefinitions = new LoadDefinitions { LoadPatterns = new() { new LoadPattern3D { Id = patternId, Label = "Dead" } } }
        };
        var session = new Model3DWorkspaceSession(document);

        var items = session.BrowserItems("n1");
        session.Select(nodeId);

        Assert.Single(items);
        Assert.Equal(nodeId, items[0].Id);
        Assert.Contains(nodeId, session.Selection);
        Assert.Equal("Node", items[0].Kind);
        Assert.Contains(session.BrowserItems(), item => item.Id == levelId && item.Kind == "Level");
        Assert.Contains(session.BrowserItems(), item => item.Id == patternId && item.Kind == "Load");
    }

    [Fact]
    public void SessionSnapAndCommandHistoryKeepDocumentAsSourceOfTruth()
    {
        var session = new Model3DWorkspaceSession();
        var nodeId = Guid.NewGuid();
        var snapped = session.Snap(new(1.47, 0.52, 2.49), .5, .1);
        var result = session.Execute(new CreateNodeCommand(nodeId, "N1", snapped.Position));

        Assert.True(result.IsValid);
        Assert.Equal(snapped.Position, session.Document.Model.Nodes.Single().Position);
        Assert.True(session.History.CanUndo);
        session.Select(nodeId);
        session.SetDisplay(display => display with { ShowLabels = false, ActiveView = "Plan XY" });
        Assert.False(session.Display.ShowLabels);
        Assert.Equal("Plan XY", session.Display.ActiveView);
        Assert.Contains(nodeId, session.Selection);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(100)]
    [InlineData(1000)]
    public void BrowserRemainsCompleteAcrossSmallMediumAndLargeModels(int nodeCount)
    {
        var nodes = Enumerable.Range(0, nodeCount).Select(index => new Node3D
        {
            Id = Guid.NewGuid(), Label = $"N{index + 1}", Position = new(index, 0, 0)
        }).ToList();
        var session = new Model3DWorkspaceSession(new ProjectDocument { Model = new Model3D { Nodes = nodes } });

        Assert.Equal(nodeCount, session.BrowserItems().Count(item => item.Kind == "Node"));
        Assert.Equal(nodes.Select(node => node.Id).ToHashSet(), session.BrowserItems().Where(item => item.Kind == "Node").Select(item => item.Id).ToHashSet());
    }

    [Fact]
    public void GosaRoundTripPreservesWorkspaceObjectIds()
    {
        var nodeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var document = new ProjectDocument { Model = new Model3D { Nodes = new() { new Node3D { Id = nodeId, Label = "N1", Position = new(1, 2, 3) } } } };
        var path = Path.Combine(Path.GetTempPath(), $"gostruct-{Guid.NewGuid():N}.gosa");
        try
        {
            var store = new GosaProjectStore();
            store.SaveAtomic(path, document);
            Assert.Equal(nodeId, store.Load(path).Model.Nodes.Single().Id);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
