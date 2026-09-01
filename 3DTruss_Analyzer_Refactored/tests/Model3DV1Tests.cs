namespace TrussAnalyzer.Tests;

using System.Text.Json;
using TrussAnalyzer.Core.Domain.V1;
using TrussAnalyzer.Core.IO;
using Xunit;

public class Model3DV1Tests
{
    [Fact]
    public void ValueDtos_HaveValueEquality_WhilePersistentIdentitySurvivesRecordUpdate()
    {
        Assert.Equal(new Point3DValue(1, 2, 3), new Point3DValue(1, 2, 3));
        Assert.Equal(new DofValues(1, 2, 3, 4, 5, 6), new DofValues(1, 2, 3, 4, 5, 6));

        var original = new Node3D { Id = Guid.NewGuid(), Label = "N1", Position = new(0, 0, 0) };
        var renamed = original with { Label = "Column base" };

        Assert.Equal(original.Id, renamed.Id);
        Assert.NotEqual(original.Label, renamed.Label);
    }

    [Fact]
    public void Identity_CopyGetsNewId_DeleteUndoAndImportPreserveId()
    {
        var document = CreateValidDocument();
        var original = document.Model.Nodes[0];
        var copied = Model3DIdentity.Copy(original);
        Assert.NotEqual(original.Id, copied.Id);

        document.Model.Nodes.Remove(original);
        document.Model.Nodes.Insert(0, original);
        Assert.Equal(original.Id, document.Model.Nodes[0].Id);

        var imported = ProjectDocumentJson.Deserialize(ProjectDocumentJson.Serialize(document));
        Assert.Equal(original.Id, imported.Model.Nodes[0].Id);
    }

    [Fact]
    public void Json_RoundTripsPolymorphicObjectsWithoutIdentityOrNumericDrift()
    {
        var document = CreateValidDocument();
        var line = document.Model.LineObjects[0];
        document.LoadDefinitions.Assignments.Add(new LineLoadAssignment3D
        {
            Id = Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            Label = "Roof UDL",
            LoadPatternId = document.LoadDefinitions.LoadPatterns[0].Id,
            LineObjectId = line.Id,
            Basis = LoadCoordinateBasis.Local,
            ForcePerLength = new Vector3DValue(0, 0, -1234.56789012345),
            StartRelativePosition = 0.125,
            EndRelativePosition = 0.875
        });

        string json = ProjectDocumentJson.Serialize(document);
        var imported = ProjectDocumentJson.Deserialize(json);

        var importedLine = Assert.IsType<Frame3D>(Assert.Single(imported.Model.LineObjects));
        var importedLoad = Assert.IsType<LineLoadAssignment3D>(Assert.Single(imported.LoadDefinitions.Assignments));
        Assert.Equal(line.Id, importedLine.Id);
        Assert.Equal(-1234.56789012345, importedLoad.ForcePerLength.Z);
        Assert.Equal(0.125, importedLoad.StartRelativePosition);
        Assert.Equal(ProjectDocument.CurrentSchemaVersion, imported.SchemaVersion);
    }

    [Theory]
    [InlineData(0, 0, 0, 5, 0, 0, 0, 0, 1)]
    [InlineData(0, 0, 0, 0, 0, 5, 0, 1, 0)]
    [InlineData(1, 2, 3, 4, 7, 11, 0, 0, 1)]
    public void LocalAxes_AreOrthonormalForHorizontalVerticalAndInclinedMembers(
        double sx, double sy, double sz, double ex, double ey, double ez, double rx, double ry, double rz)
    {
        var basis = LocalAxisBasis.Create(
            new Point3DValue(sx, sy, sz),
            new Point3DValue(ex, ey, ez),
            new LocalAxisReference { ReferenceVector = new Vector3DValue(rx, ry, rz), RollRadians = 0.17 });

        Assert.InRange(Math.Abs(basis.X.Magnitude - 1), 0, 1e-12);
        Assert.InRange(Math.Abs(basis.Y.Magnitude - 1), 0, 1e-12);
        Assert.InRange(Math.Abs(basis.Z.Magnitude - 1), 0, 1e-12);
        Assert.InRange(Math.Abs(basis.X.Dot(basis.Y)), 0, 1e-12);
        Assert.InRange(Math.Abs(basis.X.Dot(basis.Z)), 0, 1e-12);
        Assert.InRange(Math.Abs(basis.Y.Dot(basis.Z)), 0, 1e-12);
        Assert.True(basis.X.Cross(basis.Y).Dot(basis.Z) > 0.999999999999);
    }

    [Fact]
    public void Validator_ReportsNearParallelLocalAxisAndZeroLength()
    {
        var document = CreateValidDocument();
        var start = document.Model.Nodes[0];
        var end = document.Model.Nodes[1];
        document.Model.LineObjects[0] = (Frame3D)document.Model.LineObjects[0] with
        {
            LocalAxis = new LocalAxisReference { ReferenceVector = new Vector3DValue(1, 1e-14, 0) }
        };
        document.Model.Nodes[1] = end with { Position = start.Position };

        var zeroLengthIssues = new Model3DValidator().Validate(document);
        Assert.Contains(zeroLengthIssues, issue => issue.Code == ValidationCode.ZeroLength);

        document.Model.Nodes[1] = end;
        var localAxisIssues = new Model3DValidator().Validate(document);
        Assert.Contains(localAxisIssues, issue => issue.Code == ValidationCode.InvalidLocalAxis);
    }

    [Fact]
    public void Validator_ReportsDuplicateLabelsButReferencesRemainIdBased()
    {
        var document = CreateValidDocument();
        var first = document.Model.Nodes[0];
        document.Model.Nodes[1] = document.Model.Nodes[1] with { Label = first.Label };

        var issues = new Model3DValidator().Validate(document);

        Assert.Contains(issues, issue => issue.Code == ValidationCode.DuplicateLabel && issue.Severity == ValidationSeverity.Warning);
        Assert.DoesNotContain(issues, issue => issue.Code == ValidationCode.DuplicateId);
        Assert.Equal(first.Id, document.Model.LineObjects[0].StartNodeId);
    }

    [Fact]
    public void Validator_ReportsMissingReferencesInvalidPropertiesReleasesAndSprings()
    {
        var document = CreateValidDocument();
        document.Model.Materials[0] = document.Model.Materials[0] with { YoungsModulus = 0 };
        document.Model.Sections[0] = document.Model.Sections[0] with { Area = -1 };
        document.Model.Springs.Add(new SpringDefinition
        {
            Label = "Bad spring",
            Stiffness = new DofValues(UX: -1)
        });
        document.Model.LineObjects[0] = (Frame3D)document.Model.LineObjects[0] with
        {
            MaterialId = Guid.NewGuid(),
            EndRelease = new EndRelease6 { Released = new DofRestraints(true, true, true, true, true, true) }
        };

        var codes = new Model3DValidator().Validate(document).Select(issue => issue.Code).ToHashSet();

        Assert.Contains(ValidationCode.InvalidMaterial, codes);
        Assert.Contains(ValidationCode.InvalidSection, codes);
        Assert.Contains(ValidationCode.InvalidSpring, codes);
        Assert.Contains(ValidationCode.InvalidRelease, codes);
        Assert.Contains(ValidationCode.MissingReference, codes);
    }

    [Fact]
    public void Validator_ReportsCyclicConstraints()
    {
        var document = CreateValidDocument();
        var n1 = document.Model.Nodes[0].Id;
        var n2 = document.Model.Nodes[1].Id;
        document.Model.RigidLinks.Add(new RigidLink3D { Label = "R1", MasterNodeId = n1, SlaveNodeId = n2 });
        document.Model.RigidLinks.Add(new RigidLink3D { Label = "R2", MasterNodeId = n2, SlaveNodeId = n1 });

        var issues = new Model3DValidator().Validate(document);

        Assert.Contains(issues, issue => issue.Code == ValidationCode.CyclicConstraint);
    }

    [Fact]
    public void Validator_ReportsAreaObjectAsUnsupportedBeforeSolve()
    {
        var document = CreateValidDocument();
        document.Model.AreaObjects.Add(new AreaObject3D
        {
            Label = "Future shell",
            BoundaryNodeIds = new List<Guid> { document.Model.Nodes[0].Id, document.Model.Nodes[1].Id, Guid.NewGuid() },
            MaterialId = document.Model.Materials[0].Id
        });

        var issues = new Model3DValidator().Validate(document);

        Assert.Contains(issues, issue => issue.Code == ValidationCode.UnsupportedAnalysisBehavior && issue.Severity == ValidationSeverity.Warning);
        Assert.Contains(issues, issue => issue.Code == ValidationCode.MissingReference);
    }

    [Fact]
    public void ValidFixture_CoversOffsetsSpringsConstraintsUnitsAndResultSelection()
    {
        var document = CreateValidDocument();
        var errors = new Model3DValidator().Validate(document).Where(issue => issue.Severity == ValidationSeverity.Error);

        Assert.Empty(errors);
        Assert.Equal(LengthDisplayUnit.Millimeter, document.UnitPreferences.Length);
        Assert.Equal(0.1, document.Model.LineObjects[0].StartRigidOffset);
        Assert.Single(document.Model.Nodes[1].SpringIds);
        Assert.Single(document.Model.Constraints);
        Assert.Single(document.AnalysisDefinitions.ResultSelections);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{not json}")]
    [InlineData("{\"schemaVersion\":\"2.0\"}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"unknownProperty\":42}")]
    public void Json_RejectsMalformedUnknownOrFutureData(string json)
    {
        Assert.Throws<ProjectDocumentFormatException>(() => ProjectDocumentJson.Deserialize(json));
    }

    [Fact]
    public void PublishedSchemaAndExample_AreReadableAndExamplePassesV1Validation()
    {
        string schemaPath = Path.Combine(AppContext.BaseDirectory, "schema", "model3d-v1.schema.json");
        string examplePath = Path.Combine(AppContext.BaseDirectory, "examples", "model3d", "v1", "minimal-frame.json");

        using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        Assert.Equal("GOStructAnalysis ProjectDocument Model3D V1", schema.RootElement.GetProperty("title").GetString());

        var document = ProjectDocumentJson.Deserialize(File.ReadAllText(examplePath));
        var errors = new Model3DValidator().Validate(document).Where(issue => issue.Severity == ValidationSeverity.Error);
        Assert.Empty(errors);
        Assert.IsType<Frame3D>(Assert.Single(document.Model.LineObjects));
    }

    [Theory]
    [InlineData("cantilever_frame_3d.json")]
    [InlineData("mixed_steel_concrete_frame.json")]
    [InlineData("portal_frame_3d.json")]
    [InlineData("rc_rectangular_frame.json")]
    [InlineData("simple_2d_truss.json")]
    [InlineData("space_truss_3d.json")]
    [InlineData("steel_truss_tower_v2.json")]
    public void ProductRename_DoesNotBreakExistingJsonExamples(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "examples", "test_models", fileName);
        var model = StructureImporterExporter.ImportStructuralModelFromJson(File.ReadAllText(path));

        Assert.NotEmpty(model.Nodes);
        Assert.NotEmpty(model.Elements);
    }

    private static ProjectDocument CreateValidDocument()
    {
        var node1Id = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var node2Id = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var materialId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var sectionId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var supportId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var springId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        var patternId = Guid.Parse("60000000-0000-0000-0000-000000000001");
        var lineId = Guid.Parse("70000000-0000-0000-0000-000000000001");
        var combinationId = Guid.Parse("80000000-0000-0000-0000-000000000001");

        var document = new ProjectDocument
        {
            ProjectInfo = new ProjectInfo { Name = "Model3D V1 test" },
            UnitPreferences = new UnitPreferences
            {
                Length = LengthDisplayUnit.Millimeter,
                Force = ForceDisplayUnit.Kilonewton,
                Stress = StressDisplayUnit.Megapascal
            }
        };
        document.Model.Materials.Add(new Material3D
        {
            Id = materialId,
            Label = "S355",
            Kind = MaterialKind.Steel,
            YoungsModulus = 200e9,
            ShearModulus = 76.923076923e9,
            PoissonsRatio = 0.3,
            Density = 7850,
            ThermalExpansionCoefficient = 1.2e-5
        });
        document.Model.Sections.Add(new Section3D
        {
            Id = sectionId,
            Label = "Generic 300",
            Area = 0.01,
            Iy = 8e-5,
            Iz = 5e-5,
            TorsionalConstant = 1e-5,
            ShearAreaY = 0.008,
            ShearAreaZ = 0.008,
            DisplayDimensions = new Dictionary<string, double> { ["depth"] = 0.3, ["width"] = 0.2 }
        });
        document.Model.Supports.Add(new SupportDefinition
        {
            Id = supportId,
            Label = "Fixed",
            Restrained = new DofRestraints(true, true, true, true, true, true)
        });
        document.Model.Springs.Add(new SpringDefinition
        {
            Id = springId,
            Label = "KX",
            Stiffness = new DofValues(UX: 2e6)
        });
        document.Model.Nodes.Add(new Node3D
        {
            Id = node1Id,
            Label = "N1",
            Position = new Point3DValue(0, 0, 0),
            SupportId = supportId
        });
        document.Model.Nodes.Add(new Node3D
        {
            Id = node2Id,
            Label = "N2",
            Position = new Point3DValue(5, 0, 0),
            SpringIds = new List<Guid> { springId }
        });
        document.Model.LineObjects.Add(new Frame3D
        {
            Id = lineId,
            Label = "B1",
            StartNodeId = node1Id,
            EndNodeId = node2Id,
            MaterialId = materialId,
            SectionId = sectionId,
            LocalAxis = new LocalAxisReference { ReferenceVector = new Vector3DValue(0, 0, 1) },
            StartRigidOffset = 0.1,
            EndRigidOffset = 0.15,
            StartInsertionOffsetLocal = new Vector3DValue(0, 0.02, 0),
            EndRelease = new EndRelease6 { Released = new DofRestraints(RZ: true) }
        });
        document.Model.Constraints.Add(new MasterSlaveConstraint3D
        {
            Label = "Tie UX",
            MasterNodeId = node1Id,
            SlaveNodeIds = new List<Guid> { node2Id },
            CoupledDofs = new DofRestraints(UX: true)
        });
        document.LoadDefinitions.LoadPatterns.Add(new LoadPattern3D
        {
            Id = patternId,
            Label = "DL",
            Kind = LoadPatternKind.Dead,
            SelfWeightMultiplier = 1
        });
        document.LoadDefinitions.LoadCombinations.Add(new LoadCombination3D
        {
            Id = combinationId,
            Label = "1.4D",
            LoadPatternFactors = new Dictionary<Guid, double> { [patternId] = 1.4 }
        });
        document.LoadDefinitions.MassSource.LoadPatternFactors[patternId] = 1;
        document.AnalysisDefinitions.Cases.Add(new AnalysisCase3D
        {
            Label = "DL linear",
            LoadPatternId = patternId
        });
        document.AnalysisDefinitions.ResultSelections.Add(new ResultSelection3D
        {
            Label = "Strength",
            Kind = ResultSelectionKind.LoadCombination,
            SourceIds = new List<Guid> { combinationId }
        });
        return document;
    }
}
