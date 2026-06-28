namespace TrussAnalyzer.Core.Models;

public enum GridAxis
{
    X,
    Y
}

public sealed class GridLine
{
    public string Id { get; init; } = string.Empty;
    public GridAxis Axis { get; init; }
    public double Coordinate { get; init; }
}

public sealed class Story
{
    public string Id { get; init; } = string.Empty;
    public double Elevation { get; init; }
}

public sealed class BeamObject
{
    public int Id { get; init; }
    public string StoryId { get; init; } = string.Empty;
    public string StartGridXId { get; init; } = string.Empty;
    public string StartGridYId { get; init; } = string.Empty;
    public string EndGridXId { get; init; } = string.Empty;
    public string EndGridYId { get; init; } = string.Empty;
    public int MaterialId { get; init; }
    public int SectionId { get; init; }
}

public sealed class ColumnObject
{
    public int Id { get; init; }
    public string GridXId { get; init; } = string.Empty;
    public string GridYId { get; init; } = string.Empty;
    public string BaseStoryId { get; init; } = string.Empty;
    public string TopStoryId { get; init; } = string.Empty;
    public int MaterialId { get; init; }
    public int SectionId { get; init; }
}

public sealed class BuildingSupport
{
    public string GridXId { get; init; } = string.Empty;
    public string GridYId { get; init; } = string.Empty;
    public string StoryId { get; init; } = string.Empty;
    public bool ConstraintX { get; init; }
    public bool ConstraintY { get; init; }
    public bool ConstraintZ { get; init; }
    public bool ConstraintRX { get; init; }
    public bool ConstraintRY { get; init; }
    public bool ConstraintRZ { get; init; }

    public static BuildingSupport Fixed(string gridXId, string gridYId, string storyId) => new()
    {
        GridXId = gridXId,
        GridYId = gridYId,
        StoryId = storyId,
        ConstraintX = true,
        ConstraintY = true,
        ConstraintZ = true,
        ConstraintRX = true,
        ConstraintRY = true,
        ConstraintRZ = true
    };
}

public sealed class BuildingNodalLoad
{
    public string LoadCaseId { get; init; } = string.Empty;
    public string GridXId { get; init; } = string.Empty;
    public string GridYId { get; init; } = string.Empty;
    public string StoryId { get; init; } = string.Empty;
    public Vector3D Force { get; init; } = Vector3D.Zero;
    public Vector3D Moment { get; init; } = Vector3D.Zero;
}

public sealed class BuildingModel
{
    public List<GridLine> GridLines { get; init; } = new();
    public List<Story> Stories { get; init; } = new();
    public List<BeamObject> Beams { get; init; } = new();
    public List<ColumnObject> Columns { get; init; } = new();
    public List<BuildingSupport> Supports { get; init; } = new();
    public List<BuildingNodalLoad> NodalLoads { get; init; } = new();
    public List<Material> Materials { get; init; } = new();
    public List<Section> Sections { get; init; } = new();
    public List<LoadCase> LoadCases { get; init; } = new();
    public List<LoadCombination> LoadCombinations { get; init; } = new();

    public StructuralModel ToStructuralModel()
    {
        var model = new StructuralModel();
        model.Materials.AddRange(Materials);
        model.Sections.AddRange(Sections);
        model.LoadCases.AddRange(LoadCases);
        model.LoadCombinations.AddRange(LoadCombinations);

        var nodeIds = new Dictionary<BuildingPointKey, int>();

        foreach (var column in Columns)
        {
            int startNode = GetOrCreateNode(model, nodeIds, new BuildingPointKey(column.GridXId, column.GridYId, column.BaseStoryId));
            int endNode = GetOrCreateNode(model, nodeIds, new BuildingPointKey(column.GridXId, column.GridYId, column.TopStoryId));
            model.Elements.Add(new FrameElement3D(column.Id, startNode, endNode, column.MaterialId, column.SectionId));
        }

        foreach (var beam in Beams)
        {
            int startNode = GetOrCreateNode(model, nodeIds, new BuildingPointKey(beam.StartGridXId, beam.StartGridYId, beam.StoryId));
            int endNode = GetOrCreateNode(model, nodeIds, new BuildingPointKey(beam.EndGridXId, beam.EndGridYId, beam.StoryId));
            model.Elements.Add(new FrameElement3D(beam.Id, startNode, endNode, beam.MaterialId, beam.SectionId));
        }

        foreach (var support in Supports)
        {
            int nodeId = GetOrCreateNode(model, nodeIds, new BuildingPointKey(support.GridXId, support.GridYId, support.StoryId));
            var node = model.Nodes.Single(n => n.Id == nodeId);
            node.ConstraintX = support.ConstraintX;
            node.ConstraintY = support.ConstraintY;
            node.ConstraintZ = support.ConstraintZ;
            node.ConstraintRX = support.ConstraintRX;
            node.ConstraintRY = support.ConstraintRY;
            node.ConstraintRZ = support.ConstraintRZ;
        }

        foreach (var load in NodalLoads)
        {
            int nodeId = GetOrCreateNode(model, nodeIds, new BuildingPointKey(load.GridXId, load.GridYId, load.StoryId));
            model.Loads.Add(new NodalLoad
            {
                LoadCaseId = load.LoadCaseId,
                NodeId = nodeId,
                Force = load.Force,
                Moment = load.Moment
            });
        }

        return model;
    }

    private int GetOrCreateNode(
        StructuralModel model,
        Dictionary<BuildingPointKey, int> nodeIds,
        BuildingPointKey key)
    {
        if (nodeIds.TryGetValue(key, out int nodeId))
            return nodeId;

        var x = FindGridLine(key.GridXId, GridAxis.X);
        var y = FindGridLine(key.GridYId, GridAxis.Y);
        var story = FindStory(key.StoryId);
        int nextId = nodeIds.Count + 1;
        model.Nodes.Add(new Node(nextId, new Point3D(x.Coordinate, y.Coordinate, story.Elevation)));
        nodeIds[key] = nextId;
        return nextId;
    }

    private GridLine FindGridLine(string id, GridAxis axis)
    {
        return GridLines.SingleOrDefault(g => g.Axis == axis && string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Building grid line '{id}' on axis {axis} was not found.");
    }

    private Story FindStory(string id)
    {
        return Stories.SingleOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Building story '{id}' was not found.");
    }

    private readonly record struct BuildingPointKey(string GridXId, string GridYId, string StoryId);
}
