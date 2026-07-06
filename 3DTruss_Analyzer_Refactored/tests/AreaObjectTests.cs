namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Analysis.Validation;
using TrussAnalyzer.Core.IO;
using TrussAnalyzer.Core.Models;
using Xunit;

public class AreaObjectTests
{
    [Fact]
    public void AreaObject_CanRepresentSlabAndWallSeparatelyFromLineElements()
    {
        var model = CreateBaseFrameModel();
        model.Nodes.Add(new Node(3, new Point3D(2, 1, 0)));
        model.Nodes.Add(new Node(4, new Point3D(0, 1, 0)));

        model.AreaObjects.Add(AreaObject.Slab(101, "Level 1 slab", new[] { 1, 2, 3, 4 }, materialId: 1, thickness: 0.15, diaphragmId: "D1"));
        model.AreaObjects.Add(AreaObject.Wall(102, "Core wall", new[] { 1, 2, 3 }, materialId: 1, thickness: 0.2));

        Assert.Single(model.Elements);
        Assert.Equal(2, model.AreaObjects.Count);
        Assert.All(model.AreaObjects, area => Assert.True(area.IsTriangularOrQuadrilateral));
        Assert.Equal(AreaObjectAnalysisBehavior.RigidDiaphragmPlaceholder, model.AreaObjects[0].AnalysisBehavior);
    }

    [Fact]
    public void StructuralModelJson_RoundTripsAreaObjects()
    {
        var model = CreateBaseFrameModel();
        model.Nodes.Add(new Node(3, new Point3D(2, 1, 0)));
        model.Nodes.Add(new Node(4, new Point3D(0, 1, 0)));
        model.AreaObjects.Add(new AreaObject
        {
            Id = 201,
            Name = "Roof shell placeholder",
            Type = AreaObjectType.Shell,
            NodeIds = new List<int> { 1, 2, 3, 4 },
            MaterialId = 1,
            Thickness = 0.12,
            AnalysisBehavior = AreaObjectAnalysisBehavior.ShellPlaceholder,
            Description = "Serialization smoke test."
        });

        string json = StructureImporterExporter.ExportStructuralModelToJson(model);
        var imported = StructureImporterExporter.ImportStructuralModelFromJson(json);

        var area = Assert.Single(imported.AreaObjects);
        Assert.Equal(201, area.Id);
        Assert.Equal("Roof shell placeholder", area.Name);
        Assert.Equal(AreaObjectType.Shell, area.Type);
        Assert.Equal(new[] { 1, 2, 3, 4 }, area.NodeIds);
        Assert.Equal(0.12, area.Thickness, precision: 10);
        Assert.Single(imported.Elements);
    }

    [Fact]
    public void ModelValidator_ReportsAreaObjectValidationAndUnsupportedAnalysisWarning()
    {
        var model = CreateBaseFrameModel();
        model.AreaObjects.Add(new AreaObject
        {
            Id = 301,
            Name = "Invalid slab",
            Type = AreaObjectType.Slab,
            NodeIds = new List<int> { 1, 1, 99 },
            MaterialId = 99,
            Thickness = 0
        });

        var messages = new ModelValidator(model).Validate();

        Assert.Contains(messages, m => m.ObjectType == SelectedModelObjectType.AreaObject && m.Message.Contains("duplicate boundary nodes"));
        Assert.Contains(messages, m => m.ObjectType == SelectedModelObjectType.AreaObject && m.Message.Contains("missing node 99"));
        Assert.Contains(messages, m => m.ObjectType == SelectedModelObjectType.AreaObject && m.Message.Contains("missing material 99"));
        Assert.Contains(messages, m => m.ObjectType == SelectedModelObjectType.AreaObject && m.Message.Contains("positive thickness"));
        Assert.Contains(messages, m => m.Severity == "Warning" && m.Message.Contains("not included in frame analysis"));
    }

    [Fact]
    public void StructuralSolver_IgnoresAreaObjectsAndPreservesLineElementResult()
    {
        var lineOnly = CreateBaseFrameModel();
        lineOnly.Nodes.Single(n => n.Id == 2).ApplyForce(10_000, 0, 0);

        var withArea = CreateBaseFrameModel();
        withArea.Nodes.Single(n => n.Id == 2).ApplyForce(10_000, 0, 0);
        withArea.Nodes.Add(CreateFixedBoundaryNode(3, new Point3D(2, 1, 0)));
        withArea.Nodes.Add(CreateFixedBoundaryNode(4, new Point3D(0, 1, 0)));
        withArea.AreaObjects.Add(AreaObject.Slab(401, "Ignored slab", new[] { 1, 2, 3, 4 }, materialId: 1, thickness: 0.15));

        var lineOnlyResult = new StructuralSolver(lineOnly).Analyze();
        var withAreaResult = new StructuralSolver(withArea).Analyze();

        Assert.Equal(
            lineOnlyResult.NodeResults.Single(n => n.NodeId == 2).Displacement.X,
            withAreaResult.NodeResults.Single(n => n.NodeId == 2).Displacement.X,
            precision: 12);
        Assert.Equal(
            lineOnlyResult.NodeResults.Single(n => n.NodeId == 1).ReactionForce.X,
            withAreaResult.NodeResults.Single(n => n.NodeId == 1).ReactionForce.X,
            precision: 8);
        Assert.Single(withAreaResult.ElementResults);
    }

    private static StructuralModel CreateBaseFrameModel()
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(new Section
        {
            Id = 1,
            Name = "Axial frame section",
            Area = 0.003,
            Iy = 6e-6,
            Iz = 8e-6,
            J = 2e-6,
            Width = 0.2,
            Depth = 0.3
        });
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0))
        {
            ConstraintX = true,
            ConstraintY = true,
            ConstraintZ = true,
            ConstraintRX = true,
            ConstraintRY = true,
            ConstraintRZ = true
        });
        model.Nodes.Add(new Node(2, new Point3D(2, 0, 0))
        {
            ConstraintY = true,
            ConstraintZ = true,
            ConstraintRX = true,
            ConstraintRY = true,
            ConstraintRZ = true
        });
        model.Elements.Add(new FrameElement3D(1, 1, 2, materialId: 1, sectionId: 1));
        return model;
    }

    private static Node CreateFixedBoundaryNode(int id, Point3D position) => new(id, position)
    {
        ConstraintX = true,
        ConstraintY = true,
        ConstraintZ = true,
        ConstraintRX = true,
        ConstraintRY = true,
        ConstraintRZ = true
    };
}
