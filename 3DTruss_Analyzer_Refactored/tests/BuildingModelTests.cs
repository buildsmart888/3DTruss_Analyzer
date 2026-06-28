namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.IO;
using TrussAnalyzer.Core.Models;
using Xunit;

public class BuildingModelTests
{
    [Fact]
    public void BuildingModel_GeneratesSimpleOneBayOneStoryFrame()
    {
        var building = CreateOneBayOneStoryFrame();

        var model = building.ToStructuralModel();

        Assert.Equal(4, model.Nodes.Count);
        Assert.Equal(3, model.Elements.Count);
        Assert.Single(model.Materials);
        Assert.Single(model.Sections);
        Assert.All(model.Elements, element =>
        {
            Assert.Contains(model.Nodes, node => node.Id == element.StartNodeId);
            Assert.Contains(model.Nodes, node => node.Id == element.EndNodeId);
            Assert.Contains(model.Materials, material => material.Id == element.MaterialId);
            Assert.Contains(model.Sections, section => section.Id == element.SectionId);
            Assert.Equal(ElementType.Frame3D, element.Type);
        });
        Assert.Equal(2, model.Nodes.Count(node => node.IsConstrained));
    }

    [Fact]
    public void BuildingModel_GeneratedFrameCanBeAnalyzedAndExported()
    {
        var building = CreateOneBayOneStoryFrame();
        building.LoadCases.Add(new LoadCase { CaseId = "L", Name = "Lateral" });
        building.NodalLoads.Add(new BuildingNodalLoad
        {
            LoadCaseId = "L",
            GridXId = "B",
            GridYId = "1",
            StoryId = "Roof",
            Force = new Vector3D(10_000, 0, 0)
        });

        var model = building.ToStructuralModel();
        var result = new StructuralSolver(model).Analyze("L");
        string json = StructureImporterExporter.ExportStructuralModelToJson(model);
        var imported = StructureImporterExporter.ImportStructuralModelFromJson(json);

        Assert.True(result.Equilibrium.IsSatisfied);
        Assert.True(result.MaxDisplacement > 0);
        Assert.Equal(model.Nodes.Count, imported.Nodes.Count);
        Assert.Equal(model.Elements.Count, imported.Elements.Count);
        Assert.Empty(new StructuralSolver(imported).ValidateModel().Where(m => m.Severity == "Error"));
    }

    private static BuildingModel CreateOneBayOneStoryFrame()
    {
        var building = new BuildingModel();
        building.GridLines.Add(new GridLine { Id = "A", Axis = GridAxis.X, Coordinate = 0 });
        building.GridLines.Add(new GridLine { Id = "B", Axis = GridAxis.X, Coordinate = 6 });
        building.GridLines.Add(new GridLine { Id = "1", Axis = GridAxis.Y, Coordinate = 0 });
        building.Stories.Add(new Story { Id = "Base", Elevation = 0 });
        building.Stories.Add(new Story { Id = "Roof", Elevation = 4 });
        building.Materials.Add(Material.StructuralSteel with { Id = 1 });
        building.Sections.Add(Section.Generic(1, "Frame section", 0.006, 12e-6, 16e-6, 4e-6));
        building.Columns.Add(new ColumnObject
        {
            Id = 1,
            GridXId = "A",
            GridYId = "1",
            BaseStoryId = "Base",
            TopStoryId = "Roof",
            MaterialId = 1,
            SectionId = 1
        });
        building.Columns.Add(new ColumnObject
        {
            Id = 2,
            GridXId = "B",
            GridYId = "1",
            BaseStoryId = "Base",
            TopStoryId = "Roof",
            MaterialId = 1,
            SectionId = 1
        });
        building.Beams.Add(new BeamObject
        {
            Id = 3,
            StoryId = "Roof",
            StartGridXId = "A",
            StartGridYId = "1",
            EndGridXId = "B",
            EndGridYId = "1",
            MaterialId = 1,
            SectionId = 1
        });
        building.Supports.Add(BuildingSupport.Fixed("A", "1", "Base"));
        building.Supports.Add(BuildingSupport.Fixed("B", "1", "Base"));
        return building;
    }
}
