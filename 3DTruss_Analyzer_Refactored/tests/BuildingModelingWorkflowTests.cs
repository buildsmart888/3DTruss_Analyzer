using TrussAnalyzer.Core.Application;
using TrussAnalyzer.Core.Analysis;
using TrussAnalyzer.Core.Domain.V1;
using Xunit;

public sealed class BuildingModelingWorkflowTests
{
    [Fact]
    public void SingleBayGeneration_IsDeterministicAndTraceable()
    {
        var generator = new BuildingModelGenerator();
        var spec = new SingleBayBuildingSpec { Name = "Test frame", Width = 6, Depth = 4, StoryHeight = 3 };
        var first = generator.GenerateSingleBay(new ProjectDocument(), spec);
        var second = generator.GenerateSingleBay(new ProjectDocument(), spec);
        Assert.Equal(8, first.Document.Model.Nodes.Count);
        Assert.Equal(8, first.Document.Model.LineObjects.Count);
        Assert.Equal(first.Document.Model.Nodes.Select(n => n.Id), second.Document.Model.Nodes.Select(n => n.Id));
        Assert.All(first.Document.Model.LineObjects, line => Assert.Equal("BuildingGenerator", line.Source.SourceSystem));
        Assert.All(first.Document.Model.LineObjects, line => Assert.True(first.TraceMap.ContainsKey(line.Id)));
        Assert.Equal(4, first.Document.LoadDefinitions.Assignments.Count);
        var pattern = first.Document.LoadDefinitions.LoadPatterns.Single();
        var analysis = new ProjectAnalysisService().Analyze(first.Document, new ProjectAnalysisRequest(ProjectAnalysisSelectionKind.LoadPattern, pattern.Id));
        Assert.True(analysis.Succeeded, string.Join("; ", analysis.Preflight.Select(p => p.Message)));
        Assert.NotNull(analysis.Snapshot);
    }

    [Fact]
    public void FloorAreaLoad_DistributesToHorizontalMembersAsLineLoads()
    {
        var generated = new BuildingModelGenerator().GenerateMultiBayBuilding(new ProjectDocument(), "Floor test", 2, 2, 6, 4, 3, useRcSections: true).Document;
        var pattern = generated.LoadDefinitions.LoadPatterns.Single();
        var changed = new FloorLoadDistributionService().ApplyRectangularArea(generated, Guid.Parse("11111111-1111-5111-8111-111111111111"), "DL Floor", pattern.Id, 0, 12, 0, 4, 3, -2.5);
        var lines = changed.LoadDefinitions.Assignments.OfType<LineLoadAssignment3D>().ToArray();
        Assert.Equal(7, lines.Length);
        Assert.Contains(lines, line => Math.Abs(line.ForcePerLength.Z + 5.0) < 1e-6);
        Assert.Contains(lines, line => Math.Abs(line.ForcePerLength.Z + 15.0) < 1e-6);
        Assert.Single(changed.LoadDefinitions.FloorAreas);
    }

    [Fact]
    public void FloorPressure_TributaryLoads_PreserveSolverVerticalResultant()
    {
        var source = new BuildingModelGenerator().GenerateMultiBayBuilding(new ProjectDocument(), "Floor resultant", 2, 2, 6, 4, 3, useRcSections: true).Document;
        var pattern = source.LoadDefinitions.LoadPatterns.Single();
        var loaded = new FloorLoadDistributionService().ApplyRectangularArea(source, Guid.NewGuid(), "Roof pressure", pattern.Id, 0, 12, 0, 4, 3, -2.5);
        var converted = new TrussAnalyzer.Core.Domain.V1.Adapters.StructuralModelModel3DAdapter().ToStructuralModel(loaded).Model;
        var loadCase = converted.LoadCases.Single(item => item.CaseId == pattern.Source.SourceObjectId);
        loadCase.IncludeSelfWeight = false;
        converted.Loads.RemoveAll(load => load is not TrussAnalyzer.Core.Models.MemberDistributedLoad);
        var assembler = new LoadVectorAssembler(converted, new DofIndexer(converted.Nodes));
        var result = assembler.CreateResult(); assembler.AssembleInto(result, loadCase);
        var resultant = result.GlobalLoadVector.Where((_, index) => index % 6 == 2).Sum();
        var expected = converted.Loads.OfType<TrussAnalyzer.Core.Models.MemberDistributedLoad>().Sum(load => { var element = converted.Elements.Single(value => value.Id == load.ElementId); var a = converted.Nodes.Single(node => node.Id == element.StartNodeId).Position; var b = converted.Nodes.Single(node => node.Id == element.EndNodeId).Position; return load.ForcePerLength.Z * Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2) + Math.Pow(b.Z - a.Z, 2)); });
        Assert.Equal(expected, resultant, precision: 6);
    }

    [Fact]
    public void Diagnostics_ReportZeroLengthDisconnectedAndLocalAxisErrors()
    {
        var nodeA = Guid.NewGuid(); var nodeB = Guid.NewGuid(); var nodeC = Guid.NewGuid(); var material = Guid.NewGuid(); var section = Guid.NewGuid(); var member = Guid.NewGuid(); var parallel = Guid.NewGuid();
        var document = new ProjectDocument
        {
            Model = new Model3D
            {
                Nodes = new() { new Node3D { Id = nodeA, Label = "A", Position = new(0, 0, 0) }, new Node3D { Id = nodeB, Label = "B", Position = new(0, 0, 0) }, new Node3D { Id = nodeC, Label = "C", Position = new(1, 0, 0) } },
                Materials = new() { new Material3D { Id = material, Label = "Steel", YoungsModulus = 200e9, ShearModulus = 80e9, PoissonsRatio = .3, Density = 7850 } },
                Sections = new() { new Section3D { Id = section, Label = "Bar", Area = .001, Iy = 1e-6, Iz = 1e-6, TorsionalConstant = 1e-6 } },
                LineObjects = new() { new Frame3D { Id = member, Label = "Zero", StartNodeId = nodeA, EndNodeId = nodeB, MaterialId = material, SectionId = section }, new Frame3D { Id = parallel, Label = "Parallel", StartNodeId = nodeA, EndNodeId = nodeC, MaterialId = material, SectionId = section, LocalAxis = new LocalAxisReference { ReferenceVector = new(1, 0, 0) } } }
            }
        };
        var diagnostics = new BuildingModelDiagnostics().Analyze(document);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "DUPLICATE-NODE");
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "ZERO-LENGTH");
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "LOCAL-AXIS-PARALLEL");
        Assert.DoesNotContain(diagnostics.Diagnostics, d => d.Code == "DISCONNECTED-NODE"); // connected endpoints are not disconnected
    }

    [Fact]
    public void Transforms_PreserveSourceAndCreateDeterministicCopies()
    {
        var generated = new BuildingModelGenerator().GenerateSingleBay(new ProjectDocument(), new SingleBayBuildingSpec { Name = "Transform test" }).Document;
        var mirrored = BuildingModelTransforms.MirrorX(generated, 0, "mirror-test");
        var arrayed = BuildingModelTransforms.ArrayX(generated, 2, 10, "array-test");
        var stories = BuildingModelTransforms.ReplicateStories(generated, 2, 3, "story-test");
        Assert.Equal(generated.Model.Nodes.Count * 2, mirrored.Model.Nodes.Count);
        Assert.Equal(generated.Model.LineObjects.Count * 2, arrayed.Model.LineObjects.Count);
        Assert.Equal(generated.Model.Nodes.Count * 2, stories.Model.Nodes.Count);
        Assert.All(mirrored.Model.LineObjects.Skip(generated.Model.LineObjects.Count), l => Assert.Equal("BuildingGenerator", l.Source.SourceSystem));
        Assert.Equal(arrayed.Model.Nodes.Select(n => n.Id), BuildingModelTransforms.ArrayX(generated, 2, 10, "array-test").Model.Nodes.Select(n => n.Id));
    }

    [Fact]
    public void DivideAndJoinMembers_KeepAssignmentsAndTraceability()
    {
        var generated = new BuildingModelGenerator().GenerateSingleBay(new ProjectDocument(), new SingleBayBuildingSpec { Name = "Divide test" }).Document;
        var member = generated.Model.LineObjects.First(l => l.Source.Notes.EndsWith("/beam", StringComparison.Ordinal));
        var divided = BuildingModelTransforms.DivideMember(generated, member.Id, 2, "divide-test");
        Assert.Equal(generated.Model.LineObjects.Count + 1, divided.Model.LineObjects.Count);
        Assert.All(divided.Model.LineObjects.Where(l => l.Label.Contains("divide")), l => Assert.Equal(member.MaterialId, l.MaterialId));
        var pieces = divided.Model.LineObjects.Where(l => l.Label.Contains("divide")).ToArray();
        var joined = BuildingModelTransforms.JoinMembers(divided, pieces[0].Id, pieces[1].Id, "join-test");
        Assert.Equal(generated.Model.LineObjects.Count, joined.Model.LineObjects.Count);
        Assert.Contains(joined.Model.LineObjects, l => l.Source.SourceObjectId.StartsWith("join:", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildingGenerators_CreatePortalRoofAndColumnStack()
    {
        var generator = new BuildingModelGenerator();
        var portal = generator.GeneratePortalFrame(new ProjectDocument(), new PortalFrameSpec { Name = "Portal", Width = 6, Depth = 4, StoryHeight = 3 });
        var roof = generator.GenerateRoofTruss(new ProjectDocument(), new RoofTrussSpec { Name = "Roof", Width = 6, Depth = 4, StoryHeight = 3 });
        var stack = generator.GenerateColumnStack(new ProjectDocument(), "Stack", 6, 4, 3, 3);
        Assert.Equal(8, portal.Document.Model.LineObjects.Count);
        Assert.Equal(9, roof.Document.Model.LineObjects.Count);
        Assert.True(stack.Document.Model.Nodes.Count > portal.Document.Model.Nodes.Count);
    }

    [Fact]
    public void MergeTrimAndWorkingPlane_PreserveValidTopology()
    {
        var generated = new BuildingModelGenerator().GenerateSingleBay(new ProjectDocument(), new SingleBayBuildingSpec { Name = "Edit test" }).Document;
        var member = generated.Model.LineObjects.First(); var trimmed = BuildingModelTransforms.TrimMember(generated, member.Id, .1, .9);
        Assert.Equal(generated.Model.LineObjects.Count, trimmed.Model.LineObjects.Count);
        var planeId = Guid.NewGuid(); var withPlane = WorkingPlaneService.Upsert(trimmed, planeId, "XY", new(0, 0, 0), new(0, 0, 1), new(1, 0, 0));
        Assert.Contains(withPlane.Model.WorkingPlanes, p => p.Id == planeId);
        var trimmedMember = trimmed.Model.LineObjects.Single(l => l.Id == member.Id);
        Assert.Throws<InvalidOperationException>(() => BuildingModelTransforms.MergeNodes(withPlane, trimmedMember.StartNodeId, trimmedMember.EndNodeId));
    }
}
