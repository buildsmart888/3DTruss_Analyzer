namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Analysis;
using TrussAnalyzer.Core.Models;
using Xunit;

public class LoadVectorAssemblerTests
{
    [Fact]
    public void AssembleInto_DefaultLoads_AddsAppliedNodeForceAndMomentAtNodeDofs()
    {
        var model = CreateFrameModel();
        var node = model.Nodes.Single(candidate => candidate.Id == 2);
        node.ApplyForce(100, 200, 300);
        node.ApplyMoment(400, 500, 600);
        var assembler = new LoadVectorAssembler(model, new DofIndexer(model.Nodes));
        var result = assembler.CreateResult();

        assembler.AssembleInto(result, loadCase: null);

        Assert.Equal(new[] { 100.0, 200.0, 300.0, 400.0, 500.0, 600.0 }, result.GlobalLoadVector.Skip(6).Take(6).ToArray());
    }

    [Fact]
    public void AssembleInto_LoadCase_AppliesCaseAndCombinationFactors()
    {
        var model = CreateFrameModel();
        var loadCase = new LoadCase
        {
            CaseId = "L",
            LoadFactor = 1.5,
            NodeForces = new Dictionary<int, ForceVector> { [2] = new(100, 0, 0) }
        };
        model.Loads.Add(new NodalLoad
        {
            LoadCaseId = "L",
            NodeId = 2,
            Force = new Vector3D(0, -50, 0),
            Moment = new Vector3D(0, 0, 10)
        });
        var assembler = new LoadVectorAssembler(model, new DofIndexer(model.Nodes));
        var result = assembler.CreateResult();

        assembler.AssembleInto(result, loadCase, combinationFactor: 2);

        Assert.Equal(300, result.GlobalLoadVector[6]);
        Assert.Equal(-150, result.GlobalLoadVector[7]);
        Assert.Equal(30, result.GlobalLoadVector[11]);
    }

    [Fact]
    public void AssembleInto_MemberPointLoad_MapsGlobalLoadAndRetainsEquivalentLocalLoad()
    {
        var model = CreateFrameModel(length: 4);
        var loadCase = new LoadCase { CaseId = "P" };
        model.Loads.Add(new MemberPointLoad
        {
            LoadCaseId = "P",
            ElementId = 1,
            RelativeDistance = 0.5,
            Force = new Vector3D(0, -1000, 0)
        });
        var assembler = new LoadVectorAssembler(model, new DofIndexer(model.Nodes));
        var result = assembler.CreateResult();

        assembler.AssembleInto(result, loadCase);

        var local = result.EquivalentElementLoadsLocal[1];
        Assert.Equal(-500, local[1], precision: 6);
        Assert.Equal(-500, local[5], precision: 6);
        Assert.Equal(-500, local[7], precision: 6);
        Assert.Equal(500, local[11], precision: 6);
        Assert.Equal(-500, result.GlobalLoadVector[1], precision: 6);
        Assert.Equal(-500, result.GlobalLoadVector[7], precision: 6);
    }

    [Fact]
    public void AssembleInto_DistributedLoadAndSelfWeight_PreservesEquivalentLocalLoad()
    {
        var model = CreateFrameModel(length: 4);
        var loadCase = new LoadCase { CaseId = "DL", IncludeSelfWeight = true };
        model.Loads.Add(new MemberDistributedLoad
        {
            LoadCaseId = "DL",
            ElementId = 1,
            ForcePerLength = new Vector3D(0, -1000, 0)
        });
        var assembler = new LoadVectorAssembler(model, new DofIndexer(model.Nodes));
        var result = assembler.CreateResult();

        assembler.AssembleInto(result, loadCase);

        double halfWeight = Material.StructuralSteel.Density * 0.003 * 4 * 9.81 / 2;
        var local = result.EquivalentElementLoadsLocal[1];
        Assert.Equal(-2000, local[1], precision: 6);
        Assert.Equal(-1000 * 4 * 4 / 12.0, local[5], precision: 6);
        Assert.Equal(-2000, local[7], precision: 6);
        Assert.Equal(1000 * 4 * 4 / 12.0, local[11], precision: 6);
        Assert.Equal(-2000, result.GlobalLoadVector[1], precision: 6);
        Assert.Equal(-2000, result.GlobalLoadVector[7], precision: 6);
        Assert.Equal(-halfWeight, result.GlobalLoadVector[2], precision: 6);
        Assert.Equal(-halfWeight, result.GlobalLoadVector[8], precision: 6);
    }

    [Fact]
    public void AssembleInto_LocalZDistributedLoad_UsesOppositeEndMomentSigns()
    {
        var model = CreateFrameModel(length: 4);
        var loadCase = new LoadCase { CaseId = "WZ" };
        model.Loads.Add(new MemberDistributedLoad
        {
            LoadCaseId = "WZ",
            ElementId = 1,
            Direction = LoadDirection.LocalZ,
            ForcePerLength = new Vector3D(0, 0, -1000)
        });
        var assembler = new LoadVectorAssembler(model, new DofIndexer(model.Nodes));
        var result = assembler.CreateResult();

        assembler.AssembleInto(result, loadCase);

        var local = result.EquivalentElementLoadsLocal[1];
        Assert.Equal(1000 * 4 * 4 / 12.0, local[4], precision: 6);
        Assert.Equal(-1000 * 4 * 4 / 12.0, local[10], precision: 6);
    }

    private static StructuralModel CreateFrameModel(double length = 2)
    {
        var model = new StructuralModel();
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0)));
        model.Nodes.Add(new Node(2, new Point3D(length, 0, 0)));
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(Section.Generic(1, "Frame", 0.003, 4e-6, 6e-6, 2e-6));
        model.Elements.Add(new FrameElement3D(1, 1, 2, materialId: 1, sectionId: 1));
        return model;
    }
}
