namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Analysis;
using TrussAnalyzer.Core.IO;
using TrussAnalyzer.Core.Models;
using Xunit;

public class Phase1FrameEnhancementTests
{
    [Fact]
    public void RigidEndOffsets_UseReducedFlexibleLengthForAxialDisplacement()
    {
        const double length = 4;
        const double rigidOffset = 0.5;
        const double area = 0.003;
        const double load = 12_000;
        var model = CreateAxialFrame(length, area);
        model.Elements[0] = new FrameElement3D(1, 1, 2, 1, 1)
        {
            StartRigidEndOffset = rigidOffset,
            EndRigidEndOffset = rigidOffset
        };
        model.Nodes.Single(node => node.Id == 2).ApplyForce(load, 0, 0);

        var result = new StructuralSolver(model).Analyze();

        double expected = load * (length - 2 * rigidOffset) / (200e9 * area);
        Assert.Equal(expected, result.NodeResults.Single(node => node.NodeId == 2).Displacement.X, precision: 12);
    }

    [Fact]
    public void InsertionPoint_ChangesConnectionGeometryAndKinematicTransformation()
    {
        var element = new FrameElement3D(1, 1, 2, 1, 1)
        {
            EndInsertionPointLocal = new Vector3D(0, 0.5, 0)
        };
        var geometry = FrameElementGeometryResolver.Resolve(
            element,
            new Node(1, new Point3D(0, 0, 0)),
            new Node(2, new Point3D(4, 0, 0)));

        Assert.Equal(Math.Sqrt(16.25), geometry.Length, precision: 12);
        Assert.True(Math.Abs(geometry.AnalysisTransformation[6, 11]) > 1e-12);
    }

    [Fact]
    public void TimoshenkoOption_ReducesTransverseStiffnessComparedWithEulerBernoulli()
    {
        var material = Material.StructuralSteel;
        var section = Section.Generic(1, "Deep", 0.03, 8e-4, 1e-3, 5e-4);
        var element = new FrameElement3D(1, 1, 2, 1, 1);
        var euler = new FrameElementStiffnessProvider().BuildLocalStiffness(element, material, section, length: 1);
        var timoshenko = new FrameElementStiffnessProvider(new FrameAnalysisOptions
        {
            BendingFormulation = FrameBendingFormulation.Timoshenko,
            ShearCorrectionFactorY = 5.0 / 6.0,
            ShearCorrectionFactorZ = 5.0 / 6.0
        }).BuildLocalStiffness(element, material, section, length: 1);

        Assert.True(timoshenko[1, 1] < euler[1, 1]);
        Assert.True(timoshenko[2, 2] < euler[2, 2]);
    }

    [Fact]
    public void TemperatureLoad_FixedAxialMemberDevelopsExpectedRestraintReactions()
    {
        const double area = 0.003;
        const double coefficient = 12e-6;
        const double temperatureChange = 30;
        var model = CreateAxialFrame(length: 3, area);
        ConstrainAll(model.Nodes.Single(node => node.Id == 2));
        model.LoadCases.Add(new LoadCase { CaseId = "T", Name = "Temperature", Type = LoadCaseType.Temperature });
        model.Loads.Add(new MemberTemperatureLoad
        {
            LoadCaseId = "T",
            ElementId = 1,
            TemperatureChange = temperatureChange,
            ThermalExpansionCoefficient = coefficient
        });

        var result = new StructuralSolver(model).Analyze("T");

        double expected = 200e9 * area * coefficient * temperatureChange;
        Assert.Equal(expected, result.NodeResults.Single(node => node.NodeId == 1).ReactionForce.X, precision: 6);
        Assert.Equal(-expected, result.NodeResults.Single(node => node.NodeId == 2).ReactionForce.X, precision: 6);
    }

    [Fact]
    public void ReleasedEnd_CondensesUniformLoadAndRecoversZeroReleasedMoment()
    {
        var model = CreateFixedFixedFrame(4);
        model.Elements[0] = new FrameElement3D(1, 1, 2, 1, 1)
        {
            Releases = new FrameMemberRelease { EndMomentZ = true }
        };
        model.LoadCases.Add(new LoadCase { CaseId = "W", Name = "Uniform" });
        model.Loads.Add(new MemberDistributedLoad
        {
            LoadCaseId = "W",
            ElementId = 1,
            Direction = LoadDirection.LocalY,
            ForcePerLength = new Vector3D(0, -1000, 0)
        });

        var result = new StructuralSolver(model).Analyze("W");

        Assert.Equal(0, result.ElementResults.Single().EndEndForces.Moment.Z, precision: 6);
        Assert.Equal(4000, result.NodeResults.Sum(node => node.ReactionForce.Y), precision: 6);
    }

    [Fact]
    public void TorsionCantilever_MatchesClosedFormRotation()
    {
        const double length = 3;
        const double torque = 5_000;
        var model = CreateAxialFrame(length, area: 0.003);
        var end = model.Nodes.Single(node => node.Id == 2);
        end.ConstraintX = true;
        end.ConstraintY = true;
        end.ConstraintZ = true;
        end.ConstraintRX = false;
        end.ConstraintRY = true;
        end.ConstraintRZ = true;
        end.ApplyMoment(torque, 0, 0);

        var result = new StructuralSolver(model).Analyze();

        double expected = torque * length / (Material.StructuralSteel.EffectiveShearModulus * 2e-6);
        Assert.Equal(expected, result.NodeResults.Single(node => node.NodeId == 2).Rotation.X, precision: 10);
    }

    [Fact]
    public void PortalFrameBenchmark_ResistsLateralLoadAndSatisfiesEquilibrium()
    {
        var model = CreatePortalFrame();
        model.Nodes.Single(node => node.Id == 3).ApplyForce(10_000, 0, 0);
        model.Nodes.Single(node => node.Id == 4).ApplyForce(10_000, 0, 0);

        var result = new StructuralSolver(model).Analyze();

        Assert.Equal(-20_000, result.NodeResults.Where(node => node.NodeId is 1 or 2).Sum(node => node.ReactionForce.X), precision: 6);
        Assert.True(result.NodeResults.Single(node => node.NodeId == 3).Displacement.X > 0);
        Assert.True(result.Equilibrium.IsSatisfied);
    }

    [Fact]
    public void StructuralJsonV2_RoundTripsFrameOptionsOffsetsAndTemperatureLoad()
    {
        var model = CreateAxialFrame(length: 3, area: 0.003);
        model.FrameAnalysisOptions.BendingFormulation = FrameBendingFormulation.Timoshenko;
        model.Elements[0] = new FrameElement3D(1, 1, 2, 1, 1)
        {
            StartRigidEndOffset = 0.2,
            EndRigidEndOffset = 0.3,
            StartInsertionPointLocal = new Vector3D(0, 0.1, 0),
            EndInsertionPointLocal = new Vector3D(0, 0, 0.15)
        };
        model.LoadCases.Add(new LoadCase { CaseId = "T", Type = LoadCaseType.Temperature });
        model.Loads.Add(new MemberTemperatureLoad { LoadCaseId = "T", ElementId = 1, TemperatureChange = 25, ThermalExpansionCoefficient = 12e-6 });

        var imported = StructureImporterExporter.ImportStructuralModelFromJson(StructureImporterExporter.ExportStructuralModelToJson(model));
        var element = imported.Elements.Single();
        var temperature = Assert.IsType<MemberTemperatureLoad>(imported.Loads.Single());

        Assert.Equal(FrameBendingFormulation.Timoshenko, imported.FrameAnalysisOptions.BendingFormulation);
        Assert.Equal(0.2, element.StartRigidEndOffset, precision: 12);
        Assert.Equal(0.3, element.EndRigidEndOffset, precision: 12);
        Assert.Equal(0.1, element.StartInsertionPointLocal.Y, precision: 12);
        Assert.Equal(0.15, element.EndInsertionPointLocal.Z, precision: 12);
        Assert.Equal(25, temperature.TemperatureChange, precision: 12);
    }

    private static StructuralModel CreateAxialFrame(double length, double area)
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(Section.Generic(1, "Frame", area, 4e-6, 6e-6, 2e-6));
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0))
        {
            ConstraintX = true,
            ConstraintY = true,
            ConstraintZ = true,
            ConstraintRX = true,
            ConstraintRY = true,
            ConstraintRZ = true
        });
        model.Nodes.Add(new Node(2, new Point3D(length, 0, 0))
        {
            ConstraintY = true,
            ConstraintZ = true,
            ConstraintRX = true,
            ConstraintRY = true,
            ConstraintRZ = true
        });
        model.Elements.Add(new FrameElement3D(1, 1, 2, 1, 1));
        return model;
    }

    private static StructuralModel CreateFixedFixedFrame(double length)
    {
        var model = CreateAxialFrame(length, 0.003);
        ConstrainAll(model.Nodes.Single(node => node.Id == 2));
        return model;
    }

    private static StructuralModel CreatePortalFrame()
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(Section.Generic(1, "Portal", 0.02, 8e-5, 1.2e-4, 2e-5));
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0)));
        model.Nodes.Add(new Node(2, new Point3D(6, 0, 0)));
        model.Nodes.Add(new Node(3, new Point3D(0, 0, 4)));
        model.Nodes.Add(new Node(4, new Point3D(6, 0, 4)));
        ConstrainAll(model.Nodes[0]);
        ConstrainAll(model.Nodes[1]);
        model.Elements.Add(new FrameElement3D(1, 1, 3, 1, 1));
        model.Elements.Add(new FrameElement3D(2, 2, 4, 1, 1));
        model.Elements.Add(new FrameElement3D(3, 3, 4, 1, 1));
        return model;
    }

    private static void ConstrainAll(Node node)
    {
        node.ConstraintX = true;
        node.ConstraintY = true;
        node.ConstraintZ = true;
        node.ConstraintRX = true;
        node.ConstraintRY = true;
        node.ConstraintRZ = true;
    }
}
