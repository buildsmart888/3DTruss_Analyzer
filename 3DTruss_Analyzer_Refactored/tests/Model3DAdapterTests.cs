namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Domain.V1;
using TrussAnalyzer.Core.Domain.V1.Adapters;
using TrussAnalyzer.Core.IO.Projects;
using TrussAnalyzer.Core.Models;
using Xunit;

public class Model3DAdapterTests
{
    [Fact]
    public void StructuralModelRoundTrip_PreservesFrameAnalysisParityAndLegacyIds()
    {
        var source = CreateAxialFrameModel();
        var adapter = new StructuralModelModel3DAdapter();

        var export = adapter.ToProjectDocument(source, new ProjectInfo { Name = "Parity frame" });
        var contractErrors = new Model3DValidator().Validate(export.Document).Where(issue => issue.Severity == ValidationSeverity.Error);
        var import = adapter.ToStructuralModel(export.Document);

        var expected = new StructuralSolver(source).Analyze("L");
        var actual = new StructuralSolver(import.Model).Analyze("L");

        Assert.Empty(contractErrors);
        Assert.Equal(source.Nodes.Select(node => node.Id), import.Model.Nodes.Select(node => node.Id));
        Assert.Equal(source.Elements.Select(element => element.Id), import.Model.Elements.Select(element => element.Id));
        Assert.Equal(
            expected.NodeResults.Single(node => node.NodeId == 2).Displacement.X,
            actual.NodeResults.Single(node => node.NodeId == 2).Displacement.X,
            precision: 12);
        Assert.Equal(
            expected.NodeResults.Single(node => node.NodeId == 1).ReactionForce.X,
            actual.NodeResults.Single(node => node.NodeId == 1).ReactionForce.X,
            precision: 8);
    }

    [Fact]
    public void StructuralModelExport_UsesDeterministicIdsAndValidReferenceAxisForVerticalMember()
    {
        var source = CreateAxialFrameModel();
        source.Nodes[1] = new Node(2, new Point3D(0, 0, 4))
        {
            ConstraintX = true, ConstraintY = true, ConstraintRX = true, ConstraintRY = true, ConstraintRZ = true
        };
        var adapter = new StructuralModelModel3DAdapter();

        var first = adapter.ToProjectDocument(source);
        var second = adapter.ToProjectDocument(source);
        var line = Assert.IsType<Frame3D>(Assert.Single(first.Document.Model.LineObjects));

        Assert.Equal(first.NodeIds[1], second.NodeIds[1]);
        Assert.Equal(first.ElementIds[1], second.ElementIds[1]);
        Assert.Equal(new Vector3DValue(0, 1, 0), line.LocalAxis.ReferenceVector);
        Assert.DoesNotContain(new Model3DValidator().Validate(first.Document), issue => issue.Code == ValidationCode.InvalidLocalAxis);
    }

    [Fact]
    public void StructuralModelRoundTrip_PreservesOffsetFrameDistributedLoadParity()
    {
        var source = CreateFixedFixedFrameModel();
        var adapter = new StructuralModelModel3DAdapter();
        var converted = adapter.ToStructuralModel(adapter.ToProjectDocument(source).Document).Model;

        var expected = new StructuralSolver(source).Analyze("UDL");
        var actual = new StructuralSolver(converted).Analyze("UDL");

        Assert.Equal(
            expected.NodeResults.Single(node => node.NodeId == 1).ReactionForce.Y,
            actual.NodeResults.Single(node => node.NodeId == 1).ReactionForce.Y,
            precision: 8);
        Assert.Equal(
            expected.ElementResults.Single().MomentZ,
            actual.ElementResults.Single().MomentZ,
            precision: 8);
    }

    [Fact]
    public void StructuralModelExport_ReportsEveryKnownLossyConversion()
    {
        var source = CreateAxialFrameModel();
        source.Materials[0] = source.Materials[0] with { YieldStrength = 250e6 };
        source.Sections[0] = new Section
        {
            Id = 1, Name = "RC design section", Type = SectionType.RC_Rectangular,
            Area = 0.01, Iy = 1e-4, Iz = 1e-4, J = 1e-5, RebarArea = 0.001, EffectiveDepth = 0.25
        };
        source.AreaObjects.Add(AreaObject.Slab(10, "Slab", new[] { 1, 2, 1 }, 1, 0.15));
        source.Loads.Add(new MemberPointLoad { LoadCaseId = "L", ElementId = 1, Force = new Vector3D(0, 0, -1000) });
        source.Loads.Add(new MemberTemperatureLoad { LoadCaseId = "L", ElementId = 1, TemperatureChange = 10, ThermalExpansionCoefficient = 1.2e-5 });

        var export = new StructuralModelModel3DAdapter().ToProjectDocument(source);
        var codes = export.Diagnostics.Select(diagnostic => diagnostic.Code).ToHashSet();

        Assert.Contains("SM2M3-MATERIAL-DESIGN", codes);
        Assert.Contains("SM2M3-SECTION-DESIGN", codes);
        Assert.Contains("SM2M3-AREA", codes);
        Assert.Contains("SM2M3-POINT-LOAD", codes);
        Assert.Contains("SM2M3-TEMPERATURE", codes);
    }

    [Fact]
    public void Model3DImport_PreservesMappedFramePropertiesAndReportsUnsupportedFeatures()
    {
        var source = CreateAxialFrameModel();
        source.Elements[0] = new FrameElement3D(1, 1, 2, 1, 1)
        {
            RollAngleRadians = 0.2,
            StartRigidEndOffset = 0.1,
            EndRigidEndOffset = 0.2,
            StartInsertionPointLocal = new Vector3D(0, 0.03, 0),
            Releases = new FrameMemberRelease { EndMomentZ = true }
        };
        var adapter = new StructuralModelModel3DAdapter();
        var document = adapter.ToProjectDocument(source).Document;
        document.Model.Springs.Add(new SpringDefinition { Label = "Unsupported spring", Stiffness = new DofValues(UX: 1e6) });
        document.Model.RigidLinks.Add(new RigidLink3D { Label = "Unsupported link", MasterNodeId = document.Model.Nodes[0].Id, SlaveNodeId = document.Model.Nodes[1].Id });
        var frame = Assert.IsType<Frame3D>(document.Model.LineObjects[0]);
        document.Model.LineObjects[0] = frame with { StartRelease = new EndRelease6 { Released = new DofRestraints(UX: true) } };

        var imported = adapter.ToStructuralModel(document);
        var element = Assert.IsType<FrameElement3D>(Assert.Single(imported.Model.Elements));
        var codes = imported.Diagnostics.Select(diagnostic => diagnostic.Code).ToHashSet();

        Assert.Equal(0.2, element.RollAngleRadians, precision: 12);
        Assert.Equal(0.1, element.StartRigidEndOffset, precision: 12);
        Assert.Equal(0.2, element.EndRigidEndOffset, precision: 12);
        Assert.True(element.Releases.EndMomentZ);
        Assert.Contains("M32SM-RELEASE", codes);
        Assert.Contains("M32SM-SPRING", codes);
        Assert.Contains("M32SM-RIGID-LINK", codes);
    }

    [Fact]
    public void MilestoneCPersistenceFoundation_SerializesAndReportsInMemoryMigrationWithoutChangingSource()
    {
        var source = CreateAxialFrameModel();
        var migration = new StructuralModelToModel3DMigration();
        var migrated = migration.Migrate(source);
        var serializer = new Model3DJsonProjectSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(migrated.Document));

        Assert.Equal(Model3DJsonProjectSerializer.Format, migrated.Report.TargetFormat);
        Assert.Contains(migrated.Report.Entries, entry => entry.Code == "MIGRATION-START");
        Assert.Equal(source.Nodes.Count, restored.Model.Nodes.Count);
        Assert.Equal(source.Elements.Count, restored.Model.LineObjects.Count);
        Assert.Equal(1, source.Elements[0].Id);
    }

    private static StructuralModel CreateAxialFrameModel()
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(Section.Generic(1, "Frame", 0.003, 6e-6, 8e-6, 2e-6));
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0))
        {
            ConstraintX = true, ConstraintY = true, ConstraintZ = true,
            ConstraintRX = true, ConstraintRY = true, ConstraintRZ = true
        });
        model.Nodes.Add(new Node(2, new Point3D(4, 0, 0))
        {
            ConstraintY = true, ConstraintZ = true, ConstraintRX = true, ConstraintRY = true, ConstraintRZ = true
        });
        model.Elements.Add(new FrameElement3D(1, 1, 2, 1, 1));
        model.LoadCases.Add(new LoadCase
        {
            CaseId = "L", Name = "Axial test", NodeForces = new Dictionary<int, ForceVector> { [2] = new(10_000, 0, 0) }
        });
        return model;
    }

    private static StructuralModel CreateFixedFixedFrameModel()
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(Section.Generic(1, "Frame", 0.006, 12e-6, 16e-6, 4e-6));
        foreach (var node in new[] { new Node(1, new Point3D(0, 0, 0)), new Node(2, new Point3D(6, 0, 0)) })
        {
            node.ConstraintX = true; node.ConstraintY = true; node.ConstraintZ = true;
            node.ConstraintRX = true; node.ConstraintRY = true; node.ConstraintRZ = true;
            model.Nodes.Add(node);
        }
        model.Elements.Add(new FrameElement3D(1, 1, 2, 1, 1)
        {
            StartRigidEndOffset = 0.2,
            EndRigidEndOffset = 0.2,
            StartInsertionPointLocal = new Vector3D(0, 0.02, 0),
            EndInsertionPointLocal = new Vector3D(0, 0.02, 0)
        });
        model.LoadCases.Add(new LoadCase { CaseId = "UDL", Name = "Local UDL" });
        model.Loads.Add(new MemberDistributedLoad
        {
            LoadCaseId = "UDL", ElementId = 1, Direction = LoadDirection.LocalY,
            ForcePerLength = new Vector3D(0, -5_000, 0)
        });
        return model;
    }
}
