namespace TrussAnalyzer.Core.IO.Projects;

using System.Text.Json;
using TrussAnalyzer.Core.Domain.V1;
using TrussAnalyzer.Core.IO;

/// <summary>Imports the current C# schema v2 and the legacy truss JSON path through existing readers.</summary>
public sealed class LegacyJsonProjectMigration : IProjectMigration<string>
{
    private readonly StructuralModelToModel3DMigration _structuralMigration = new();
    public string SourceFormat => "trussanalyzer.json/legacy-or-v2";

    public ProjectMigrationResult Migrate(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON content is required.", nameof(json));
        using var document = JsonDocument.Parse(json);
        string detectedFormat = DetectFormat(document.RootElement);
        var structuralModel = StructureImporterExporter.ImportStructuralModelFromJson(json);
        var result = _structuralMigration.Migrate(structuralModel);
        var entries = result.Report.Entries.ToList();
        entries.Insert(0, new MigrationEntry(MigrationEntrySeverity.Information, "MIGRATION-SOURCE", $"Imported {detectedFormat} using the existing compatibility reader."));
        return result with { Report = new ProjectMigrationReport(detectedFormat, result.Report.TargetFormat, entries) };
    }

    private static string DetectFormat(JsonElement root)
    {
        if (root.TryGetProperty("schemaVersion", out var schema) && schema.ValueKind == JsonValueKind.Number && schema.GetInt32() == 2)
            return "trussanalyzer.structuralmodel.json/2";
        return "trussanalyzer.truss.json/legacy";
    }
}
