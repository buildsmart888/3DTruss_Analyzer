namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Application;
using TrussAnalyzer.Core.Domain.V1.Adapters;
using TrussAnalyzer.Core.IO.Projects;
using TrussAnalyzer.Core.Models;
using System.IO.Compression;
using Xunit;

public sealed class MilestoneHResultExportTests
{
    [Fact]
    public void ResultExport_UsesStableUnitsAndSnapshotValues()
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(Section.Generic(1, "Bar", .01, 1e-6, 1e-6, 1e-6));
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0)) { ConstraintX = true, ConstraintY = true, ConstraintZ = true, ConstraintRX = true, ConstraintRY = true, ConstraintRZ = true });
        model.Nodes.Add(new Node(2, new Point3D(2, 0, 0)) { ConstraintY = true, ConstraintZ = true, ConstraintRX = true, ConstraintRY = true, ConstraintRZ = true });
        model.Elements.Add(new TrussElement(1, 1, 2, 1, 1));
        model.LoadCases.Add(new LoadCase { CaseId = "L", NodeForces = { [2] = new ForceVector(1000, 0, 0) } });
        var document = new StructuralModelModel3DAdapter().ToProjectDocument(model).Document;
        var pattern = document.LoadDefinitions.LoadPatterns.Single(p => p.Source.SourceObjectId == "L");
        var result = new ProjectAnalysisService().Analyze(document, new(ProjectAnalysisSelectionKind.LoadPattern, pattern.Id));
        var snapshot = Assert.IsType<AnalysisSnapshot>(result.Snapshot);

        var exporter = new AnalysisResultExportService();
        var csv = exporter.ToCsv(snapshot);
        var json = exporter.ToJson(snapshot);
        Assert.Contains("DX (m)", csv);
        Assert.Contains("N (N)", csv);
        Assert.Contains(snapshot.DocumentChecksum, csv);
        Assert.Contains(snapshot.DocumentChecksum, json);

        var captured = new ProjectAnalysisService().CaptureResult(document,
            new ProjectAnalysisRequest(ProjectAnalysisSelectionKind.LoadPattern, pattern.Id),
            new StructuralSolver(new StructuralModelModel3DAdapter().ToStructuralModel(document).Model).Analyze("L"));
        Assert.Equal(snapshot.SelectionId, captured.SelectionId);
        Assert.Equal(snapshot.DocumentChecksum, captured.DocumentChecksum);
        Assert.Equal(snapshot.Nodes.Count, captured.Nodes.Count);
        Assert.Equal(snapshot.Members.Count, captured.Members.Count);
    }

    [Fact]
    public void XlsxExport_WritesOpenXmlWorkbookWithResultSheet()
    {
        using var directory = new TemporaryDirectory();
        var snapshot = new AnalysisSnapshot { DocumentChecksum = "ABC", SolverName = "Dense" };
        var path = Path.Combine(directory.Path, "result.xlsx");
        new AnalysisResultXlsxExporter().Save(snapshot, path);
        using var archive = ZipFile.OpenRead(path);
        Assert.Contains(archive.Entries, entry => entry.FullName == "[Content_Types].xml");
        Assert.Contains(archive.Entries, entry => entry.FullName == "xl/worksheets/sheet1.xml");
        var sheet = Read(archive.GetEntry("xl/worksheets/sheet1.xml")!);
        Assert.Contains("ABC", sheet);
        Assert.Contains("r=\"A1\"", sheet);
        Assert.DoesNotContain("r=\"A\" ", sheet);
    }

    [Fact]
    public void SnapshotPdfExport_WritesValidObjectOffsetsAndSelectedResultMetadata()
    {
        var snapshot = new AnalysisSnapshot
        {
            DocumentChecksum = "CHECKSUM-123",
            SelectionId = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
            SolverName = "QualifiedDense"
        };
        byte[] bytes = new AnalysisSnapshotPdfExporter().Generate(snapshot);
        string pdf = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", pdf);
        Assert.Contains("CHECKSUM-123", pdf);
        Assert.Contains("QualifiedDense", pdf);
        var lines = pdf.Split('\n');
        int xref = Array.IndexOf(lines, "xref");
        Assert.True(xref > 0);
        foreach (string entry in lines.Skip(xref + 3).Take(5))
        {
            int offset = int.Parse(entry[..10], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Matches("[1-5] 0 obj", pdf[offset..]);
        }
    }

    [Fact]
    public void WorkflowRegression_EditAnalyzeExportSaveReopenPreservesIdentityAndProvenance()
    {
        var model = new StructuralModel();
        model.Materials.Add(Material.StructuralSteel with { Id = 1 });
        model.Sections.Add(Section.Generic(1, "Bar", .01, 1e-6, 1e-6, 1e-6));
        model.Nodes.Add(new Node(1, new Point3D(0, 0, 0)) { ConstraintX = true, ConstraintY = true, ConstraintZ = true, ConstraintRX = true, ConstraintRY = true, ConstraintRZ = true });
        model.Nodes.Add(new Node(2, new Point3D(2, 0, 0)) { ConstraintY = true, ConstraintZ = true, ConstraintRX = true, ConstraintRY = true, ConstraintRZ = true });
        model.Elements.Add(new TrussElement(1, 1, 2, 1, 1));
        model.LoadCases.Add(new LoadCase { CaseId = "L", NodeForces = { [2] = new ForceVector(1000, 0, 0) } });
        var adapter = new StructuralModelModel3DAdapter();
        var document = adapter.ToProjectDocument(model).Document;
        var originalIds = document.Model.Nodes.Select(value => value.Id).Concat(document.Model.LineObjects.Select(value => value.Id)).ToArray();

        var movedNodes = document.Model.Nodes.Select(value => value.Source.SourceObjectId == "2"
            ? value with { Position = value.Position with { X = 3 } }
            : value).ToList();
        var edited = document with { Model = document.Model with { Nodes = movedNodes } };
        var pattern = edited.LoadDefinitions.LoadPatterns.Single(value => value.Source.SourceObjectId == "L");
        var analysis = new ProjectAnalysisService().Analyze(edited, new(ProjectAnalysisSelectionKind.LoadPattern, pattern.Id));
        var snapshot = Assert.IsType<AnalysisSnapshot>(analysis.Snapshot);
        var export = new AnalysisResultExportService();

        Assert.Contains(snapshot.DocumentChecksum, export.ToCsv(snapshot));
        Assert.Contains(snapshot.DocumentChecksum, export.ToJson(snapshot));
        Assert.Contains(snapshot.DocumentChecksum, System.Text.Encoding.ASCII.GetString(new AnalysisSnapshotPdfExporter().Generate(snapshot)));

        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "workflow.gosa");
        new GosaProjectStore().SaveAtomic(path, edited);
        var reopened = new GosaProjectStore().Load(path);
        var reopenedIds = reopened.Model.Nodes.Select(value => value.Id).Concat(reopened.Model.LineObjects.Select(value => value.Id)).ToArray();
        Assert.Equal(originalIds, reopenedIds);
        var reopenedAnalysis = new ProjectAnalysisService().Analyze(reopened, new(ProjectAnalysisSelectionKind.LoadPattern, pattern.Id));
        Assert.Equal(snapshot.DocumentChecksum, Assert.IsType<AnalysisSnapshot>(reopenedAnalysis.Snapshot).DocumentChecksum);
    }

    private static string Read(ZipArchiveEntry entry) { using var reader = new StreamReader(entry.Open()); return reader.ReadToEnd(); }
    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GOStructH", Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
