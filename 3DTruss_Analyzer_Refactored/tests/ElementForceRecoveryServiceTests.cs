namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Analysis;
using TrussAnalyzer.Core.Models;
using Xunit;

public class ElementForceRecoveryServiceTests
{
    [Fact]
    public void Recover_AxialDisplacement_ReturnsAxialForceAndStress()
    {
        var model = CreateFrameModel(length: 4);
        var dofIndexer = new DofIndexer(model.Nodes);
        var displacements = new double[dofIndexer.TotalDof];
        displacements[6] = 0.001;
        var service = new ElementForceRecoveryService(model, dofIndexer, new FrameElementStiffnessProvider());

        var result = service.Recover(model.Elements.Single(), displacements, new Dictionary<int, double[]>());

        double expectedAxial = Material.StructuralSteel.YoungsModulus * 0.003 / 4 * 0.001;
        Assert.Equal(expectedAxial, result.AxialForce, precision: 6);
        Assert.Equal(expectedAxial / 0.003, result.Stress, precision: 6);
        Assert.Equal(-expectedAxial, result.StartEndForces.Force.X, precision: 6);
        Assert.Equal(expectedAxial, result.EndEndForces.Force.X, precision: 6);
    }

    [Fact]
    public void Recover_DistributedLoad_SubtractsEquivalentLocalLoadAndBuildsStations()
    {
        var model = CreateFrameModel(length: 4);
        model.ResultStationCount = 9;
        var loadCase = new LoadCase { CaseId = "W" };
        model.Loads.Add(new MemberDistributedLoad
        {
            LoadCaseId = "W",
            ElementId = 1,
            ForcePerLength = new Vector3D(0, -1000, 0)
        });
        var dofIndexer = new DofIndexer(model.Nodes);
        var loadAssembler = new LoadVectorAssembler(model, dofIndexer);
        var loadAssembly = loadAssembler.CreateResult();
        loadAssembler.AssembleInto(loadAssembly, loadCase);
        var service = new ElementForceRecoveryService(model, dofIndexer, new FrameElementStiffnessProvider());

        var result = service.Recover(model.Elements.Single(), new double[dofIndexer.TotalDof], loadAssembly.EquivalentElementLoadsLocal);

        Assert.Equal(2000, result.ShearY, precision: 6);
        Assert.Equal(1000 * 4 * 4 / 12.0, result.MomentZ, precision: 6);
        Assert.Equal(9, result.StationResults.Count);
        Assert.Equal(0.5, result.StationResults[4].RelativePosition, precision: 12);
        Assert.Equal(0, result.StationResults[4].ShearY, precision: 6);
    }

    [Fact]
    public void Recover_FullLengthUniformLocalYLoad_BuildsExactShearAndParabolicMomentStations()
    {
        var model = CreateFrameModel(length: 4);
        model.ResultStationCount = 9;
        var loadCase = new LoadCase { CaseId = "W" };
        model.Loads.Add(new MemberDistributedLoad
        {
            LoadCaseId = "W",
            ElementId = 1,
            Direction = LoadDirection.LocalY,
            ForcePerLength = new Vector3D(0, -1000, 0)
        });
        var dofIndexer = new DofIndexer(model.Nodes);
        var loadAssembly = new LoadVectorAssembler(model, dofIndexer).CreateResult();
        new LoadVectorAssembler(model, dofIndexer).AssembleInto(loadAssembly, loadCase);
        var service = new ElementForceRecoveryService(model, dofIndexer, new FrameElementStiffnessProvider());

        var result = service.Recover(
            model.Elements.Single(),
            new double[dofIndexer.TotalDof],
            loadAssembly.EquivalentElementLoadsLocal,
            loadAssembly.MemberDiagramLoadsLocal);

        var midspan = result.StationResults.Single(station => Math.Abs(station.RelativePosition - 0.5) < 1e-12);
        Assert.Equal(0, midspan.ShearY, precision: 6);
        Assert.Equal(-1000 * 4 * 4 / 24.0, midspan.MomentZ, precision: 6);
        Assert.Equal(1000 * 4 * 4 / 12.0, result.StationResults.First().MomentZ, precision: 6);
        Assert.Equal(1000 * 4 * 4 / 12.0, result.StationResults.Last().MomentZ, precision: 6);
    }

    [Fact]
    public void Recover_MidspanPointLocalYLoad_BuildsLeftAndRightShearStations()
    {
        var model = CreateFrameModel(length: 4);
        model.ResultStationCount = 5;
        var loadCase = new LoadCase { CaseId = "P" };
        model.Loads.Add(new MemberPointLoad
        {
            LoadCaseId = "P",
            ElementId = 1,
            RelativeDistance = 0.5,
            Direction = LoadDirection.LocalY,
            Force = new Vector3D(0, -1000, 0)
        });
        var dofIndexer = new DofIndexer(model.Nodes);
        var loadAssembly = new LoadVectorAssembler(model, dofIndexer).CreateResult();
        new LoadVectorAssembler(model, dofIndexer).AssembleInto(loadAssembly, loadCase);
        var service = new ElementForceRecoveryService(model, dofIndexer, new FrameElementStiffnessProvider());

        var result = service.Recover(
            model.Elements.Single(),
            new double[dofIndexer.TotalDof],
            loadAssembly.EquivalentElementLoadsLocal,
            loadAssembly.MemberDiagramLoadsLocal);

        var midspanStations = result.StationResults.Where(station => Math.Abs(station.RelativePosition - 0.5) < 1e-12).ToList();
        Assert.Equal(2, midspanStations.Count);
        Assert.Equal(DiagramStationSide.Left, midspanStations[0].DiagramSide);
        Assert.Equal(DiagramStationSide.Right, midspanStations[1].DiagramSide);
        Assert.Equal(500, midspanStations[0].ShearY, precision: 6);
        Assert.Equal(-500, midspanStations[1].ShearY, precision: 6);
        Assert.Equal(-500, midspanStations[0].MomentZ, precision: 6);
        Assert.Equal(-500, midspanStations[1].MomentZ, precision: 6);
    }

    [Fact]
    public void Recover_FullLengthUniformLocalZLoad_BuildsExactShearAndParabolicMomentStations()
    {
        var model = CreateFrameModel(length: 4);
        model.ResultStationCount = 9;
        var loadCase = new LoadCase { CaseId = "WZ" };
        model.Loads.Add(new MemberDistributedLoad
        {
            LoadCaseId = "WZ",
            ElementId = 1,
            Direction = LoadDirection.LocalZ,
            ForcePerLength = new Vector3D(0, 0, -1000)
        });
        var dofIndexer = new DofIndexer(model.Nodes);
        var loadAssembler = new LoadVectorAssembler(model, dofIndexer);
        var loadAssembly = loadAssembler.CreateResult();
        loadAssembler.AssembleInto(loadAssembly, loadCase);
        var service = new ElementForceRecoveryService(model, dofIndexer, new FrameElementStiffnessProvider());

        var result = service.Recover(
            model.Elements.Single(),
            new double[dofIndexer.TotalDof],
            loadAssembly.EquivalentElementLoadsLocal,
            loadAssembly.MemberDiagramLoadsLocal);

        var midspan = result.StationResults.Single(station => Math.Abs(station.RelativePosition - 0.5) < 1e-12);
        Assert.Equal(0, midspan.ShearZ, precision: 6);
        Assert.Equal(1000 * 4 * 4 / 24.0, midspan.MomentY, precision: 6);
        Assert.Equal(-1000 * 4 * 4 / 12.0, result.StationResults.First().MomentY, precision: 6);
        Assert.Equal(-1000 * 4 * 4 / 12.0, result.StationResults.Last().MomentY, precision: 6);
    }

    [Fact]
    public void Recover_MidspanLocalZMoment_BuildsLeftAndRightMomentJump()
    {
        var model = CreateFrameModel(length: 4);
        model.ResultStationCount = 5;
        var loadCase = new LoadCase { CaseId = "MZ" };
        model.Loads.Add(new MemberPointLoad
        {
            LoadCaseId = "MZ",
            ElementId = 1,
            RelativeDistance = 0.5,
            Direction = LoadDirection.LocalY,
            Moment = new Vector3D(0, 0, 1000)
        });
        var dofIndexer = new DofIndexer(model.Nodes);
        var loadAssembler = new LoadVectorAssembler(model, dofIndexer);
        var loadAssembly = loadAssembler.CreateResult();
        loadAssembler.AssembleInto(loadAssembly, loadCase);
        var service = new ElementForceRecoveryService(model, dofIndexer, new FrameElementStiffnessProvider());

        var result = service.Recover(
            model.Elements.Single(),
            new double[dofIndexer.TotalDof],
            loadAssembly.EquivalentElementLoadsLocal,
            loadAssembly.MemberDiagramLoadsLocal);

        var midspanStations = result.StationResults.Where(station => Math.Abs(station.RelativePosition - 0.5) < 1e-12).ToList();
        Assert.Equal(-500, midspanStations[0].MomentZ, precision: 6);
        Assert.Equal(500, midspanStations[1].MomentZ, precision: 6);
    }

    private static StructuralModel CreateFrameModel(double length)
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
