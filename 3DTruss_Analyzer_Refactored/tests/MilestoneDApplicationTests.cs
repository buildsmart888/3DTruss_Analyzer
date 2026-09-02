namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Application;
using TrussAnalyzer.Core.Domain.V1.Adapters;
using TrussAnalyzer.Core.Models;
using Xunit;

public sealed class MilestoneDApplicationTests
{
    [Fact]
    public void ProjectAnalysisService_ProducesGuidKeyedTraceableSnapshot()
    {
        var source = CreateStableTruss();
        var converted = new StructuralModelModel3DAdapter().ToProjectDocument(source);
        var pattern = converted.Document.LoadDefinitions.LoadPatterns.Single(pattern => pattern.Source.SourceObjectId == "L");

        var result = new ProjectAnalysisService().Analyze(converted.Document,
            new ProjectAnalysisRequest(ProjectAnalysisSelectionKind.LoadPattern, pattern.Id));

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Preflight.Select(message => $"{message.Severity} {message.Code}: {message.Message}")));
        var snapshot = Assert.IsType<AnalysisSnapshot>(result.Snapshot);
        Assert.Equal(pattern.Id, snapshot.SelectionId);
        Assert.Equal(ProjectAnalysisSelectionKind.LoadPattern, snapshot.SelectionKind);
        Assert.Equal(64, snapshot.DocumentChecksum.Length);
        Assert.NotEmpty(snapshot.SolverName);
        Assert.Equal(2, snapshot.Nodes.Count);
        Assert.Equal(converted.NodeIds[2], snapshot.Nodes.Single(node => node.NodeId == converted.NodeIds[2]).NodeId);
        Assert.Single(snapshot.Members);
        Assert.Equal(converted.ElementIds[1], snapshot.Members.Single().LineObjectId);
        Assert.True(snapshot.Equilibrium.IsSatisfied);
    }

    [Fact]
    public void ProjectAnalysisService_BlocksUnsupportedModel3DBeforeSolving()
    {
        var converted = new StructuralModelModel3DAdapter().ToProjectDocument(CreateStableTruss());
        converted.Document.Model.AreaObjects.Add(new TrussAnalyzer.Core.Domain.V1.AreaObject3D
        {
            Id = Guid.NewGuid(), Label = "Storage only", BoundaryNodeIds = new List<Guid> { converted.NodeIds[1], converted.NodeIds[2], converted.NodeIds[1] }
        });
        var pattern = converted.Document.LoadDefinitions.LoadPatterns.Single(pattern => pattern.Source.SourceObjectId == "L");

        var result = new ProjectAnalysisService().Analyze(converted.Document,
            new ProjectAnalysisRequest(ProjectAnalysisSelectionKind.LoadPattern, pattern.Id));

        Assert.False(result.Succeeded);
        Assert.Null(result.Snapshot);
        Assert.Contains(result.Preflight, message => message.Code == "UnsupportedAnalysisBehavior" && message.Severity == "Error");
    }

    [Fact]
    public void ProjectCommandHistory_UndoRedoMarksExpectedModelEdit()
    {
        int value = 0;
        var history = new ProjectCommandHistory();
        history.Execute(new DelegateProjectCommand("Increment", () => value++, () => value--));
        Assert.Equal(1, value);
        Assert.True(history.CanUndo);
        history.Undo();
        Assert.Equal(0, value);
        Assert.True(history.CanRedo);
        history.Redo();
        Assert.Equal(1, value);
    }

    [Fact]
    public void ApplicationSettingsStore_AndRecentProjects_PreserveShellState()
    {
        using var directory = new TestDirectory();
        string path = Path.Combine(directory.Path, "settings.json");
        var settings = new ApplicationSettings
        {
            Language = "th", ActiveStage = "Results", WindowWidth = 1280, WindowHeight = 800,
            RecentProjectPaths = RecentProjectList.Add(Array.Empty<string>(), Path.Combine(directory.Path, "a.gosa"))
        };
        var store = new ApplicationSettingsStore();
        store.Save(path, settings);
        var loaded = store.Load(path);
        Assert.Equal("th", loaded.Language);
        Assert.Equal("Results", loaded.ActiveStage);
        Assert.Equal(1280, loaded.WindowWidth);
        Assert.Single(loaded.RecentProjectPaths);
    }

    [Fact]
    public void AutosaveScheduler_TriggerRunsWithoutWaitingForTimer()
    {
        int calls = 0;
        using var scheduler = new AutosaveScheduler();
        scheduler.Start(TimeSpan.FromMinutes(1), () => calls++);
        scheduler.Trigger();
        Assert.Equal(1, calls);
        Assert.True(scheduler.IsRunning);
    }

    [Fact]
    public void PhysicalModelEditor_PreservesIdentityAndProducesValidFrameGroup()
    {
        var editor = new PhysicalModelEditor();
        var material = new TrussAnalyzer.Core.Domain.V1.Material3D { Id = Guid.NewGuid(), Label = "Steel", YoungsModulus = 200e9, ShearModulus = 77e9, PoissonsRatio = .3, Density = 7850 };
        var section = new TrussAnalyzer.Core.Domain.V1.Section3D { Id = Guid.NewGuid(), Label = "W", Area = .01, Iy = 1e-5, Iz = 1e-5, TorsionalConstant = 1e-6 };
        var document = new TrussAnalyzer.Core.Domain.V1.ProjectDocument { Model = new TrussAnalyzer.Core.Domain.V1.Model3D { Materials = new() { material }, Sections = new() { section } } };
        var first = editor.AddNode(document, "N1", new TrussAnalyzer.Core.Domain.V1.Point3DValue(0, 0, 0), Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var second = editor.AddNode(first, "N2", new TrussAnalyzer.Core.Domain.V1.Point3DValue(4, 0, 0), Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var framed = editor.AddFrame(second, "B1", second.Model.Nodes[0].Id, second.Model.Nodes[1].Id, material.Id, section.Id, id: Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var grouped = editor.CreateGroup(framed, "Level 1", new[] { framed.Model.LineObjects[0].Id });
        Assert.Equal("N1", grouped.Model.Nodes[0].Label);
        Assert.Single(grouped.Model.LineObjects);
        Assert.Single(grouped.Model.Groups);
        Assert.DoesNotContain(new TrussAnalyzer.Core.Domain.V1.Model3DValidator().Validate(grouped), issue => issue.Severity == TrussAnalyzer.Core.Domain.V1.ValidationSeverity.Error);
    }

    [Fact]
    public void PhysicalModelEditor_UpdatesNodeAndPresentationWithoutChangingEngineeringIdentity()
    {
        var editor = new PhysicalModelEditor();
        var nodeId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var groupId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var original = editor.AddNode(new TrussAnalyzer.Core.Domain.V1.ProjectDocument(), "N1", new(0, 0, 0), nodeId);
        var grouped = editor.CreateGroup(original, "Selected", new[] { nodeId }, groupId);
        var edited = editor.UpdateNode(grouped, nodeId, "N-EDIT", new(2.5, -1, 3));
        var colored = editor.SetGroupDisplayColor(edited, groupId, "#e66928");

        Assert.Equal(nodeId, colored.Model.Nodes.Single().Id);
        Assert.Equal("N-EDIT", colored.Model.Nodes.Single().Label);
        Assert.Equal(new TrussAnalyzer.Core.Domain.V1.Point3DValue(2.5, -1, 3), colored.Model.Nodes.Single().Position);
        Assert.Equal("#E66928", colored.PresentationSettings.GroupDisplayColors[groupId]);
        Assert.Equal(grouped.Model.Groups.Single().ObjectIds, colored.Model.Groups.Single().ObjectIds);
    }

    private static StructuralModel CreateStableTruss()
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(Section.Generic(1, "Bar", 0.01, 1e-6, 1e-6, 1e-6));
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0)) { ConstraintX = true, ConstraintY = true, ConstraintZ = true, ConstraintRX = true, ConstraintRY = true, ConstraintRZ = true });
        model.Nodes.Add(new Node(2, new Point3D(2, 0, 0)) { ConstraintY = true, ConstraintZ = true, ConstraintRX = true, ConstraintRY = true, ConstraintRZ = true });
        model.Elements.Add(new TrussElement(1, 1, 2, 1, 1));
        model.LoadCases.Add(new LoadCase { CaseId = "L", Name = "Test load", NodeForces = { [2] = new ForceVector(1000, 0, 0) } });
        return model;
    }

    private sealed class TestDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GOStructAnalysisTests", Guid.NewGuid().ToString("N"));
        public TestDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
    }
}
