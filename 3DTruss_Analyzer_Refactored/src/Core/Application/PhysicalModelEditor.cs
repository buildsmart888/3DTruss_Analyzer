namespace TrussAnalyzer.Core.Application;

using TrussAnalyzer.Core.Domain.V1;

/// <summary>GUID-preserving physical-model edits. UI tools create commands around these pure document transforms.</summary>
public sealed class PhysicalModelEditor
{
    public ProjectDocument AddNode(ProjectDocument document, string label, Point3DValue position, Guid? id = null)
    {
        EnsureLabel(label);
        var model = CopyModel(document);
        model.Nodes.Add(new Node3D { Id = id ?? Guid.NewGuid(), Label = label.Trim(), Position = position });
        return WithModel(document, model);
    }

    public ProjectDocument MoveNode(ProjectDocument document, Guid nodeId, Point3DValue position)
    {
        var model = CopyModel(document);
        int index = model.Nodes.FindIndex(node => node.Id == nodeId);
        if (index < 0) throw new InvalidOperationException($"Node '{nodeId}' was not found.");
        model.Nodes[index] = model.Nodes[index] with { Position = position };
        return WithModel(document, model);
    }

    public ProjectDocument AddFrame(ProjectDocument document, string label, Guid startNodeId, Guid endNodeId, Guid materialId, Guid sectionId, bool truss = false, Guid? id = null)
    {
        EnsureLabel(label);
        if (startNodeId == endNodeId) throw new ArgumentException("A member must connect two different nodes.");
        var model = CopyModel(document);
        if (!model.Nodes.Any(node => node.Id == startNodeId) || !model.Nodes.Any(node => node.Id == endNodeId)) throw new InvalidOperationException("Member endpoint node was not found.");
        if (!model.Materials.Any(material => material.Id == materialId)) throw new InvalidOperationException("Member material was not found.");
        if (!model.Sections.Any(section => section.Id == sectionId)) throw new InvalidOperationException("Member section was not found.");
        LineObject3D line = truss ? new Truss3D() : new Frame3D();
        model.LineObjects.Add(line with { Id = id ?? Guid.NewGuid(), Label = label.Trim(), StartNodeId = startNodeId, EndNodeId = endNodeId, MaterialId = materialId, SectionId = sectionId });
        return WithModel(document, model);
    }

    public ProjectDocument CreateGroup(ProjectDocument document, string label, IEnumerable<Guid> objectIds, Guid? id = null)
    {
        EnsureLabel(label);
        var model = CopyModel(document);
        var ids = objectIds?.Distinct().ToList() ?? throw new ArgumentNullException(nameof(objectIds));
        var known = model.Nodes.Select(node => node.Id).Concat(model.LineObjects.Select(line => line.Id)).ToHashSet();
        if (ids.Any(value => !known.Contains(value))) throw new InvalidOperationException("Groups may only reference existing nodes or line objects.");
        model.Groups.Add(new ModelGroup3D { Id = id ?? Guid.NewGuid(), Label = label.Trim(), ObjectIds = ids });
        return WithModel(document, model);
    }

    private static ProjectDocument WithModel(ProjectDocument document, Model3D model) => document with
    {
        Model = model,
        AuditMetadata = document.AuditMetadata with { ModifiedUtc = DateTimeOffset.UtcNow }
    };

    private static Model3D CopyModel(ProjectDocument document) => document.Model with
    {
        Nodes = document.Model.Nodes.ToList(), LineObjects = document.Model.LineObjects.ToList(), AreaObjects = document.Model.AreaObjects.ToList(),
        Materials = document.Model.Materials.ToList(), Sections = document.Model.Sections.ToList(), Supports = document.Model.Supports.ToList(),
        Springs = document.Model.Springs.ToList(), PrescribedMovements = document.Model.PrescribedMovements.ToList(), RigidLinks = document.Model.RigidLinks.ToList(),
        Constraints = document.Model.Constraints.ToList(), Levels = document.Model.Levels.ToList(), Grids = document.Model.Grids.ToList(), Groups = document.Model.Groups.ToList()
    };

    private static void EnsureLabel(string label) { if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A physical object label is required.", nameof(label)); }
}
