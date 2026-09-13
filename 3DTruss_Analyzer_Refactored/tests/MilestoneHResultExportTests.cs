namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Application;
using TrussAnalyzer.Core.Domain.V1.Adapters;
using TrussAnalyzer.Core.Models;
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
    }
}
