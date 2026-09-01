namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Analysis;
using TrussAnalyzer.Core.Models;
using Xunit;

public class DofIndexerTests
{
    [Fact]
    public void DofIndexer_AssignsSixDofsPerNodeInModelOrder()
    {
        var nodes = new[]
        {
            new Node(10, new Point3D(0, 0, 0)),
            new Node(25, new Point3D(1, 0, 0)),
            new Node(7, new Point3D(2, 0, 0))
        };

        var indexer = new DofIndexer(nodes);

        Assert.Equal(18, indexer.TotalDof);
        Assert.Equal(0, indexer.GetNodeDofBase(10));
        Assert.Equal(6, indexer.GetNodeDofBase(25));
        Assert.Equal(12, indexer.GetNodeDofBase(7));
    }

    [Fact]
    public void DofIndexer_BuildsElementDofMapFromStartAndEndNodes()
    {
        var nodes = new[]
        {
            new Node(1, new Point3D(0, 0, 0)),
            new Node(2, new Point3D(1, 0, 0)),
            new Node(3, new Point3D(2, 0, 0))
        };
        var element = new FrameElement3D(5, 3, 1, materialId: 1, sectionId: 1);

        var map = new DofIndexer(nodes).GetElementDofMap(element);

        Assert.Equal(new[] { 12, 13, 14, 15, 16, 17, 0, 1, 2, 3, 4, 5 }, map);
    }

    [Fact]
    public void DofIndexer_ReportsConstrainedDofs()
    {
        var node = new Node(4, new Point3D(0, 0, 0))
        {
            ConstraintX = true,
            ConstraintZ = true,
            ConstraintRY = true
        };
        var indexer = new DofIndexer(new[]
        {
            new Node(1, new Point3D(0, 0, 0)),
            node
        });

        Assert.Equal(3, indexer.ConstrainedDof);
        Assert.Equal(new[] { 6, 8, 10 }, indexer.GetConstrainedDofs(node).ToArray());
    }

    [Fact]
    public void DofIndexer_MissingNodeThrowsClearError()
    {
        var indexer = new DofIndexer(new[] { new Node(1, new Point3D(0, 0, 0)) });

        var error = Assert.Throws<KeyNotFoundException>(() => indexer.GetNodeDofBase(99));
        Assert.Contains("Node 99", error.Message);
    }
}
