namespace TrussAnalyzer.Core.IO.Projects;

using TrussAnalyzer.Core.Domain.V1;
using TrussAnalyzer.Core.Domain.V1.Adapters;
using TrussAnalyzer.Core.Models;

/// <summary>Milestone C boundary. Package, atomic-save, backup, and recovery implementations follow this contract.</summary>
public interface IProjectSerializer
{
    string FormatId { get; }
    string Serialize(ProjectDocument document);
    ProjectDocument Deserialize(string payload);
}

public sealed class Model3DJsonProjectSerializer : IProjectSerializer
{
    public const string Format = "gostructanalysis.model3d.json/1.0";
    public string FormatId => Format;
    public string Serialize(ProjectDocument document) => ProjectDocumentJson.Serialize(document);
    public ProjectDocument Deserialize(string payload) => ProjectDocumentJson.Deserialize(payload);
}

public enum MigrationEntrySeverity { Information, Warning, Error }

public sealed record MigrationEntry(MigrationEntrySeverity Severity, string Code, string Message);

public sealed record ProjectMigrationReport(
    string SourceFormat,
    string TargetFormat,
    IReadOnlyList<MigrationEntry> Entries)
{
    public bool HasErrors => Entries.Any(entry => entry.Severity == MigrationEntrySeverity.Error);
    public bool HasWarnings => Entries.Any(entry => entry.Severity == MigrationEntrySeverity.Warning);
}

public sealed record ProjectMigrationResult(ProjectDocument Document, ProjectMigrationReport Report);

public interface IProjectMigration<in TSource>
{
    string SourceFormat { get; }
    ProjectMigrationResult Migrate(TSource source);
}

/// <summary>
/// Deterministic in-memory migration boundary for the current C# model. It does not replace a source
/// file; callers must show the report and use Milestone C atomic persistence before writing any output.
/// </summary>
public sealed class StructuralModelToModel3DMigration : IProjectMigration<StructuralModel>
{
    private readonly StructuralModelModel3DAdapter _adapter = new();
    public string SourceFormat => "trussanalyzer.structuralmodel/in-memory-v2";

    public ProjectMigrationResult Migrate(StructuralModel source)
    {
        var converted = _adapter.ToProjectDocument(source);
        var entries = converted.Diagnostics
            .Select(diagnostic => new MigrationEntry(MapSeverity(diagnostic.Severity), diagnostic.Code, diagnostic.Message))
            .ToList();
        entries.Insert(0, new MigrationEntry(MigrationEntrySeverity.Information, "MIGRATION-START", "Converted current StructuralModel data into Model3D V1 in memory; no source file was changed."));
        return new ProjectMigrationResult(converted.Document, new ProjectMigrationReport(SourceFormat, Model3DJsonProjectSerializer.Format, entries));
    }

    private static MigrationEntrySeverity MapSeverity(AdapterDiagnosticSeverity severity) => severity switch
    {
        AdapterDiagnosticSeverity.Error => MigrationEntrySeverity.Error,
        AdapterDiagnosticSeverity.Warning => MigrationEntrySeverity.Warning,
        _ => MigrationEntrySeverity.Information
    };
}
