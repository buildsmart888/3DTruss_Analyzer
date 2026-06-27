namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.IO;
using TrussAnalyzer.Core.Models;
using Xunit;

public class StructuralSolverMvpTests
{
    [Fact]
    public void FrameCantilever_TipLoad_MatchesClosedFormDeflection()
    {
        double length = 3.0;
        double load = -10_000;
        var model = CreateCantileverFrame(length);
        model.Nodes.Single(n => n.Id == 2).ApplyForce(0, load, 0);

        var result = new StructuralSolver(model).Analyze();

        double expected = load * Math.Pow(length, 3) / (3 * 200e9 * 8e-6);
        double actual = result.NodeResults.Single(n => n.NodeId == 2).Displacement.Y;
        Assert.Equal(expected, actual, precision: 6);
        Assert.True(result.Equilibrium.IsSatisfied);
    }

    [Fact]
    public void FrameElement_AxialOnly_MatchesClosedFormBarDisplacement()
    {
        double length = 2.5;
        double area = 0.003;
        double force = 50_000;
        var model = CreateCantileverFrame(length, area);
        var free = model.Nodes.Single(n => n.Id == 2);
        free.ConstraintY = true;
        free.ConstraintZ = true;
        free.ConstraintRX = true;
        free.ConstraintRY = true;
        free.ConstraintRZ = true;
        free.ApplyForce(force, 0, 0);

        var result = new StructuralSolver(model).Analyze();

        double expected = force * length / (200e9 * area);
        Assert.Equal(expected, result.NodeResults.Single(n => n.NodeId == 2).Displacement.X, precision: 10);
        Assert.Equal(-force, result.NodeResults.Single(n => n.NodeId == 1).ReactionForce.X, precision: 6);
    }

    [Fact]
    public void NodalMomentLoad_ProducesSupportMomentReaction()
    {
        var model = CreateCantileverFrame(4);
        model.LoadCases.Add(new LoadCase { CaseId = "M", Name = "Moment" });
        model.Loads.Add(new NodalLoad
        {
            LoadCaseId = "M",
            NodeId = 2,
            Moment = new Vector3D(0, 0, 5_000)
        });

        var result = new StructuralSolver(model).Analyze("M");

        Assert.True(Math.Abs(result.NodeResults.Single(n => n.NodeId == 1).ReactionMoment.Z) > 4_999);
        Assert.True(result.Equilibrium.IsSatisfied);
    }

    [Fact]
    public void MemberDistributedLoad_AndSelfWeight_AreIncludedInLoadCase()
    {
        double length = 5;
        var model = CreateCantileverFrame(length);
        model.LoadCases.Add(new LoadCase { CaseId = "DL", Name = "Dead", IncludeSelfWeight = true });
        model.Loads.Add(new MemberDistributedLoad
        {
            LoadCaseId = "DL",
            ElementId = 1,
            ForcePerLength = new Vector3D(0, 0, -100)
        });

        var result = new StructuralSolver(model).Analyze("DL");

        double selfWeight = 7850 * 0.003 * length * 9.81;
        double expected = 100 * length + selfWeight;
        Assert.InRange(result.NodeResults.Single(n => n.NodeId == 1).ReactionForce.Z, expected - 1e-6, expected + 1e-6);
    }

    [Fact]
    public void LoadCombination_IncludesNodalMemberAndSelfWeightLoads()
    {
        var model = CreateCantileverFrame(2);
        model.LoadCases.Add(new LoadCase { CaseId = "DL", Name = "Dead", IncludeSelfWeight = true });
        model.LoadCases.Add(new LoadCase { CaseId = "LL", Name = "Live" });
        model.Loads.Add(new NodalLoad { LoadCaseId = "LL", NodeId = 2, Force = new Vector3D(0, -1000, 0) });
        model.LoadCombinations.Add(new LoadCombination
        {
            CombinationId = "C1",
            Name = "1.2D+1.6L",
            LoadCases = new Dictionary<string, double> { ["DL"] = 1.2, ["LL"] = 1.6 }
        });

        var result = new StructuralSolver(model).AnalyzeCombination("C1");

        Assert.Equal(1600, result.NodeResults.Single(n => n.NodeId == 1).ReactionForce.Y, precision: 5);
        Assert.True(result.NodeResults.Single(n => n.NodeId == 1).ReactionForce.Z > 0);
    }

    [Fact]
    public void SectionFactory_ComputesRectangularProperties()
    {
        var section = Section.Rectangular(1, "R", 0.3, 0.5);

        Assert.Equal(0.15, section.Area, precision: 10);
        Assert.Equal(0.5 * Math.Pow(0.3, 3) / 12, section.Iy, precision: 10);
        Assert.Equal(0.3 * Math.Pow(0.5, 3) / 12, section.Iz, precision: 10);
    }

    [Fact]
    public void SteelDesignChecks_ReportOkAndNgUtilization()
    {
        var okModel = CreateCantileverFrame(1);
        okModel.Nodes.Single(n => n.Id == 2).ApplyForce(1_000, 0, 0);
        var okResult = new StructuralSolver(okModel).Analyze();

        var ngModel = CreateCantileverFrame(1, area: 1e-5);
        var free = ngModel.Nodes.Single(n => n.Id == 2);
        free.ConstraintY = true;
        free.ConstraintZ = true;
        free.ConstraintRX = true;
        free.ConstraintRY = true;
        free.ConstraintRZ = true;
        free.ApplyForce(100_000, 0, 0);
        var ngResult = new StructuralSolver(ngModel).Analyze();

        Assert.Contains(okResult.DesignChecks, c => c.Status == DesignCheckStatus.OK);
        Assert.Contains(ngResult.DesignChecks, c => c.Status == DesignCheckStatus.NG);
    }

    [Fact]
    public void ConcreteFlexureWithoutRebar_ReturnsMissingData()
    {
        var model = CreateCantileverFrame(2);
        model.Materials[0] = Material.Concrete30MPa with { Id = 1 };
        model.Sections[0] = Section.RcRectangular(1, "RC", 0.3, 0.5);
        model.Nodes.Single(n => n.Id == 2).ApplyForce(0, -1000, 0);

        var result = new StructuralSolver(model).Analyze();

        Assert.Contains(result.DesignChecks, c => c.CheckType == "RC flexure" && c.Status == DesignCheckStatus.MissingData);
    }

    [Fact]
    public void StructuralJsonV2_RoundTrip_PreservesCoreCollections()
    {
        var model = CreateCantileverFrame(2);
        model.LoadCases.Add(new LoadCase { CaseId = "L", Name = "Live" });
        model.Loads.Add(new NodalLoad { LoadCaseId = "L", NodeId = 2, Force = new Vector3D(0, -1000, 0) });

        string json = StructureImporterExporter.ExportStructuralModelToJson(model);
        var imported = StructureImporterExporter.ImportStructuralModelFromJson(json);

        Assert.Equal(2, imported.Nodes.Count);
        Assert.Single(imported.Elements);
        Assert.Single(imported.Materials);
        Assert.Single(imported.Sections);
        Assert.Single(imported.Loads);
        Assert.True(new StructuralSolver(imported).Analyze("L").Equilibrium.IsSatisfied);
    }

    [Fact]
    public void LocalAxes_RollAngle_RotatesLocalYZAxes()
    {
        var axes = StructuralSolver.GetLocalAxes(new Point3D(0, 0, 0), new Point3D(1, 0, 0), Math.PI / 2);

        Assert.Equal(1, axes.XAxis.X, precision: 10);
        Assert.Equal(1, axes.YAxis.Z, precision: 10);
        Assert.Equal(-1, axes.ZAxis.Y, precision: 10);
    }

    [Fact]
    public void FrameReleaseValidation_WarnsWhenBothEndsReleased()
    {
        var model = CreateCantileverFrame(2);
        model.Elements.Clear();
        model.Elements.Add(new FrameElement3D
        {
            Id = 1,
            StartNodeId = 1,
            EndNodeId = 2,
            MaterialId = 1,
            SectionId = 1,
            Releases = new FrameMemberRelease { StartMomentZ = true, EndMomentZ = true }
        });

        var messages = new StructuralSolver(model).ValidateModel();

        Assert.Contains(messages, m => m.Severity == "Warning" && m.Message.Contains("both Mz ends released"));
    }

    [Fact]
    public void DistributedLoad_RecoversFixedEndMoments()
    {
        var model = CreateFixedFixedFrame(4);
        model.LoadCases.Add(new LoadCase { CaseId = "W", Name = "Uniform" });
        model.Loads.Add(new MemberDistributedLoad
        {
            LoadCaseId = "W",
            ElementId = 1,
            ForcePerLength = new Vector3D(0, -1000, 0)
        });

        var result = new StructuralSolver(model).Analyze("W");
        var element = result.ElementResults.Single();

        Assert.Equal(1000 * 4 * 4 / 12.0, element.MomentZ, precision: 6);
        Assert.Equal(4000, result.NodeResults.Sum(n => n.ReactionForce.Y), precision: 6);
    }

    [Fact]
    public void MemberPointLoad_RecoversMidspanFixedEndMoments()
    {
        var model = CreateFixedFixedFrame(4);
        model.LoadCases.Add(new LoadCase { CaseId = "P", Name = "Point" });
        model.Loads.Add(new MemberPointLoad
        {
            LoadCaseId = "P",
            ElementId = 1,
            RelativeDistance = 0.5,
            Force = new Vector3D(0, -1000, 0)
        });

        var result = new StructuralSolver(model).Analyze("P");
        var element = result.ElementResults.Single();

        Assert.Equal(1000 * 4 / 8.0, element.MomentZ, precision: 6);
        Assert.Equal(1000, result.NodeResults.Sum(n => n.ReactionForce.Y), precision: 6);
    }

    [Fact]
    public void SolverDiagnostics_ReportDofAndSolverPath()
    {
        var model = CreateCantileverFrame(2);
        model.Nodes.Single(n => n.Id == 2).ApplyForce(1000, 0, 0);

        var result = new StructuralSolver(model).Analyze();

        Assert.Equal(12, result.Diagnostics.TotalDof);
        Assert.Equal(6, result.Diagnostics.ConstrainedDof);
        Assert.Contains("Dense", result.Diagnostics.SolverName);
        Assert.True(result.Diagnostics.MatrixDensity > 0);
    }

    [Fact]
    public void ElementForceResult_ReportsNamedEndForcesAndSummary()
    {
        var model = CreateFixedFixedFrame(4);
        model.LoadCases.Add(new LoadCase { CaseId = "W", Name = "Uniform" });
        model.Loads.Add(new MemberDistributedLoad
        {
            LoadCaseId = "W",
            ElementId = 1,
            ForcePerLength = new Vector3D(0, -1000, 0)
        });

        var result = new StructuralSolver(model).Analyze("W");
        var element = result.ElementResults.Single();

        Assert.NotEqual(Vector3D.Zero, element.StartEndForces.Force);
        Assert.NotEqual(Vector3D.Zero, element.EndEndForces.Force);
        Assert.True(result.Summary.MaxShearY > 0);
        Assert.True(result.Summary.MaxMomentZ > 0);
    }

    [Fact]
    public void LocalAxes_AreRightHandedForVerticalMember()
    {
        var axes = StructuralSolver.GetLocalAxes(new Point3D(0, 0, 0), new Point3D(0, 0, 5));

        Assert.Equal(1, axes.XAxis.Magnitude, precision: 10);
        Assert.Equal(1, axes.YAxis.Magnitude, precision: 10);
        Assert.Equal(1, axes.ZAxis.Magnitude, precision: 10);
        Assert.Equal(1, axes.XAxis.Cross(axes.YAxis).Dot(axes.ZAxis), precision: 10);
    }

    [Fact]
    public void StructuralJsonV2_PreservesCoordinateAndDisplayMetadata()
    {
        var model = CreateCantileverFrame(2);
        model.ActiveLoadCaseId = "LL";
        model.DisplaySettings.DiagramMode = ResultDiagramMode.MomentDiagram;
        model.DisplaySettings.Layers.Loads = false;

        var imported = StructureImporterExporter.ImportStructuralModelFromJson(
            StructureImporterExporter.ExportStructuralModelToJson(model));

        Assert.Equal(CoordinateConvention.RightHanded_ZUp, imported.CoordinateSystem);
        Assert.Equal("LL", imported.ActiveLoadCaseId);
        Assert.Equal(ResultDiagramMode.MomentDiagram, imported.DisplaySettings.DiagramMode);
        Assert.False(imported.DisplaySettings.Layers.Loads);
    }

    [Fact]
    public void StructuralJsonV2_RoundTrip_PreservesReleaseRollAndDesignSettings()
    {
        var model = CreateCantileverFrame(2);
        model.DesignSettings = new DesignSettings { CompressionEffectiveLengthFactor = 1.2, SteelResistanceFactor = 0.85 };
        model.Elements.Clear();
        model.Elements.Add(new FrameElement3D
        {
            Id = 1,
            StartNodeId = 1,
            EndNodeId = 2,
            MaterialId = 1,
            SectionId = 1,
            RollAngleRadians = 0.25,
            Releases = new FrameMemberRelease { EndMomentY = true }
        });

        var imported = StructureImporterExporter.ImportStructuralModelFromJson(StructureImporterExporter.ExportStructuralModelToJson(model));
        var element = imported.Elements.Single();

        Assert.Equal(0.25, element.RollAngleRadians, precision: 10);
        Assert.True(element.Releases.EndMomentY);
        Assert.Equal(1.2, imported.DesignSettings.CompressionEffectiveLengthFactor, precision: 10);
        Assert.Equal(0.85, imported.DesignSettings.SteelResistanceFactor, precision: 10);
    }

    [Fact]
    public void StructuralJsonImporter_SupportsV1TrussJson()
    {
        var solver = new TrussSolver();
        solver.AddNode(new Node(1, new Point3D(0, 0, 0)) { ConstraintX = true, ConstraintY = true, ConstraintZ = true });
        var node = new Node(2, new Point3D(1, 0, 0)) { ConstraintY = true, ConstraintZ = true };
        node.ApplyForce(1000, 0, 0);
        solver.AddNode(node);
        solver.AddElement(new Element(1, 1, 2, 0.001, Material.StructuralSteel));

        var imported = StructureImporterExporter.ImportStructuralModelFromJson(StructureImporterExporter.ExportToJson(solver));

        Assert.Equal(2, imported.Nodes.Count);
        Assert.IsType<TrussElement>(imported.Elements.Single());
    }

    [Fact]
    public void ValidationReportsMissingMaterialSectionAndSupport()
    {
        var model = new StructuralModel();
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0)));
        model.Nodes.Add(new Node(2, new Point3D(1, 0, 0)));
        model.Elements.Add(new FrameElement3D(1, 1, 2, 99, 88));

        var messages = new StructuralSolver(model).ValidateModel();

        Assert.Contains(messages, m => m.Message.Contains("No supports"));
        Assert.Contains(messages, m => m.Message.Contains("missing material"));
        Assert.Contains(messages, m => m.Message.Contains("missing section"));
    }

    private static StructuralModel CreateCantileverFrame(double length, double area = 0.003)
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(new Section
        {
            Id = 1,
            Name = "Generic frame",
            Type = SectionType.Generic,
            Area = area,
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
        model.Nodes.Add(new Node(2, new Point3D(length, 0, 0)));
        model.Elements.Add(new FrameElement3D(1, 1, 2, 1, 1));
        return model;
    }

    private static StructuralModel CreateFixedFixedFrame(double length)
    {
        var model = CreateCantileverFrame(length);
        var end = model.Nodes.Single(n => n.Id == 2);
        end.ConstraintX = true;
        end.ConstraintY = true;
        end.ConstraintZ = true;
        end.ConstraintRX = true;
        end.ConstraintRY = true;
        end.ConstraintRZ = true;
        return model;
    }
}
