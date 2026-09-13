namespace TrussAnalyzer.Core.Application;

using TrussAnalyzer.Core.Domain.V1;

/// <summary>Single source-of-truth session for the Physical workspace.</summary>
public sealed class Model3DWorkspaceSession
{
    private readonly PhysicalModelSnapper _snapper = new();
    private readonly HashSet<Guid> _selection = new();

    public Model3DWorkspaceSession(ProjectDocument? document = null)
        => History = new ProjectDocumentCommandHistory(document ?? new ProjectDocument());

    public ProjectDocumentCommandHistory History { get; }
    public ProjectDocument Document => History.Current;
    public IReadOnlySet<Guid> Selection => _selection;
    public PhysicalWorkspaceDisplay Display { get; private set; } = new();
    public event EventHandler? Changed;

    public ProjectCommandValidation Execute(IProjectDocumentCommand command)
    {
        var result = History.Execute(command);
        if (result.IsValid) Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    public void Select(Guid objectId, bool additive = false)
    {
        if (!additive) _selection.Clear();
        if (Document.Model.Nodes.Any(node => node.Id == objectId) || Document.Model.LineObjects.Any(line => line.Id == objectId) || Document.Model.Groups.Any(group => group.Id == objectId)) _selection.Add(objectId);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSelection() { _selection.Clear(); Changed?.Invoke(this, EventArgs.Empty); }

    public PhysicalSnapResult Snap(Point3DValue requested, double gridSpacing = 1, double endpointTolerance = .15)
        => _snapper.Snap(Document, requested, gridSpacing, endpointTolerance);

    public ProjectCommandValidation CreateNode(string label, Point3DValue requested, double gridSpacing = 1, double endpointTolerance = .15, Guid? nodeId = null)
    {
        var snap = Snap(requested, gridSpacing, endpointTolerance);
        return Execute(new CreateNodeCommand(nodeId ?? Guid.NewGuid(), label, snap.Position));
    }

    public ProjectCommandValidation MoveNode(Guid nodeId, Point3DValue requested, double gridSpacing = 1, double endpointTolerance = .15)
        => Execute(new MoveNodeCommand(nodeId, Snap(requested, gridSpacing, endpointTolerance).Position));

    public ProjectCommandValidation DeleteNode(Guid nodeId, bool deleteConnectedMembers = false)
        => Execute(new DeleteNodeCommand(nodeId, deleteConnectedMembers));

    public ProjectCommandValidation DeleteMember(Guid memberId) => Execute(new DeleteMemberCommand(memberId));

    public ProjectCommandValidation ApplyTransform(string name, Func<ProjectDocument, ProjectDocument> transform)
        => Execute(new TransformWorkspaceCommand(name, transform));

    public void SetDisplay(Func<PhysicalWorkspaceDisplay, PhysicalWorkspaceDisplay> update)
    {
        Display = update(Display) ?? throw new ArgumentNullException(nameof(update));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<PhysicalWorkspaceItem> BrowserItems(string? query = null)
    {
        var items = Document.Model.Levels.Select(level => new PhysicalWorkspaceItem("Level", level.Id, level.Label))
            .Concat(Document.Model.Grids.Select(grid => new PhysicalWorkspaceItem("Grid", grid.Id, grid.Label)))
            .Concat(Document.Model.Nodes.Select(node => new PhysicalWorkspaceItem("Node", node.Id, node.Label)))
            .Concat(Document.Model.LineObjects.Select(line => new PhysicalWorkspaceItem(line is Truss3D ? "Truss" : "Frame", line.Id, line.Label)))
            .Concat(Document.Model.Materials.Select(material => new PhysicalWorkspaceItem("Material", material.Id, material.Label)))
            .Concat(Document.Model.Sections.Select(section => new PhysicalWorkspaceItem("Section", section.Id, section.Label)))
            .Concat(Document.Model.Groups.Select(group => new PhysicalWorkspaceItem("Group", group.Id, group.Label)))
            .Concat(Document.LoadDefinitions.LoadPatterns.Select(pattern => new PhysicalWorkspaceItem("Load", pattern.Id, pattern.Label)))
            .Concat(Document.LoadDefinitions.LoadCombinations.Select(combo => new PhysicalWorkspaceItem("Combination", combo.Id, combo.Label)));
        return string.IsNullOrWhiteSpace(query) ? items.ToArray() : items.Where(item => item.Label.Contains(query, StringComparison.OrdinalIgnoreCase) || item.Kind.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}

public sealed record PhysicalWorkspaceItem(string Kind, Guid Id, string Label);

public sealed record PhysicalWorkspaceDisplay
{
    public bool ShowGrid { get; init; } = true;
    public bool ShowLabels { get; init; } = true;
    public bool ShowLoads { get; init; } = true;
    public bool ShowSupports { get; init; } = true;
    public double Transparency { get; init; }
    public string ActiveView { get; init; } = "Isometric";
}

internal sealed record TransformWorkspaceCommand(string CommandName, Func<ProjectDocument, ProjectDocument> Transform) : IProjectDocumentCommand
{
    public string Name => CommandName;
    public ProjectDocument Apply(ProjectDocument document) => Transform(document);
}
