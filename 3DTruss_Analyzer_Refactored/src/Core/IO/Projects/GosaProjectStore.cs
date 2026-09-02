namespace TrussAnalyzer.Core.IO.Projects;

using System.IO.Compression;
using TrussAnalyzer.Core.Domain.V1;

public interface IProjectFileStore
{
    void SaveAtomic(string path, ProjectDocument document);
    ProjectDocument Load(string path);
    void SaveAutosave(string projectPath, ProjectDocument document);
    ProjectRecoveryResult RecoverLatest(string projectPath);
}

public sealed record ProjectRecoveryResult(ProjectDocument Document, string SourcePath, bool Recovered, IReadOnlyList<string> RejectedPaths);

/// <summary>
/// Versioned .gosa ZIP package storage. The package has one canonical project.json payload today;
/// attachments/results are intentionally deferred until their own versioned contracts exist.
/// </summary>
public sealed class GosaProjectStore : IProjectFileStore
{
    public const string FileExtension = ".gosa";
    public const string PackageFormat = "gostructanalysis.gosa/1";
    public const string ProjectEntryName = "project.json";
    public const string ManifestEntryName = "manifest.json";

    public void SaveAtomic(string path, ProjectDocument document)
    {
        ValidatePath(path);
        ArgumentNullException.ThrowIfNull(document);
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Project path requires a directory.");
        Directory.CreateDirectory(directory);

        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            WritePackage(temporaryPath, document);
            _ = Load(temporaryPath); // A package is never promoted until it has a readable, valid payload.
            if (File.Exists(fullPath))
                File.Copy(fullPath, BackupPath(fullPath), overwrite: true);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public ProjectDocument Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Project package was not found.", path);
        try
        {
            using var archive = ZipFile.OpenRead(path);
            var manifest = archive.GetEntry(ManifestEntryName) ?? throw new ProjectDocumentFormatException(".gosa manifest.json is missing.");
            using (var manifestReader = new StreamReader(manifest.Open()))
            {
                string manifestPayload = manifestReader.ReadToEnd();
                if (!manifestPayload.Contains(PackageFormat, StringComparison.Ordinal))
                    throw new ProjectDocumentFormatException("Unsupported .gosa package format.");
            }
            var project = archive.GetEntry(ProjectEntryName) ?? throw new ProjectDocumentFormatException(".gosa project.json is missing.");
            using var reader = new StreamReader(project.Open());
            return ProjectDocumentJson.Deserialize(reader.ReadToEnd());
        }
        catch (InvalidDataException exception)
        {
            throw new ProjectDocumentFormatException($"Invalid .gosa package: {exception.Message}", exception);
        }
    }

    public void SaveAutosave(string projectPath, ProjectDocument document) => SaveAtomic(AutosavePath(projectPath), document);

    public ProjectRecoveryResult RecoverLatest(string projectPath)
    {
        string fullPath = Path.GetFullPath(projectPath);
        var candidates = new[] { fullPath, AutosavePath(fullPath), BackupPath(fullPath) }
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
        var rejected = new List<string>();
        foreach (string candidate in candidates)
        {
            try
            {
                var document = Load(candidate);
                return new ProjectRecoveryResult(document, candidate, !string.Equals(candidate, fullPath, StringComparison.OrdinalIgnoreCase), rejected);
            }
            catch (ProjectDocumentFormatException)
            {
                rejected.Add(candidate);
            }
        }
        throw new ProjectDocumentFormatException($"No valid project, autosave, or backup snapshot could be recovered for '{fullPath}'.");
    }

    public static string BackupPath(string projectPath) => AppendProjectSuffix(projectPath, ".bak");
    public static string AutosavePath(string projectPath) => AppendProjectSuffix(projectPath, ".autosave");

    private static void WritePackage(string path, ProjectDocument document)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var manifest = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        using (var writer = new StreamWriter(manifest.Open()))
            writer.Write($"{{\"packageFormat\":\"{PackageFormat}\",\"projectSchemaVersion\":\"{ProjectDocument.CurrentSchemaVersion}\"}}");
        var project = archive.CreateEntry(ProjectEntryName, CompressionLevel.Optimal);
        using var projectWriter = new StreamWriter(project.Open());
        projectWriter.Write(ProjectDocumentJson.Serialize(document));
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Project path is required.", nameof(path));
        if (!string.Equals(Path.GetExtension(path), FileExtension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Project path must use the {FileExtension} extension.", nameof(path));
    }

    private static string AppendProjectSuffix(string projectPath, string suffix)
    {
        string fullPath = Path.GetFullPath(projectPath);
        string? directory = Path.GetDirectoryName(fullPath);
        return Path.Combine(directory ?? string.Empty, $"{Path.GetFileNameWithoutExtension(fullPath)}{suffix}{FileExtension}");
    }
}
