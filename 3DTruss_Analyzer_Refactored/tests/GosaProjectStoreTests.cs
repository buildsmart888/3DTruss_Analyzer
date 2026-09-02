namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Domain.V1;
using TrussAnalyzer.Core.IO.Projects;
using Xunit;

public class GosaProjectStoreTests
{
    [Fact]
    public void SaveAtomic_RoundTripsPackageAndRetainsPreviousSnapshotAsBackup()
    {
        using var directory = new TestDirectory();
        string path = Path.Combine(directory.Path, "tower.gosa");
        var store = new GosaProjectStore();

        store.SaveAtomic(path, CreateDocument("Revision A"));
        store.SaveAtomic(path, CreateDocument("Revision B"));

        Assert.Equal("Revision B", store.Load(path).ProjectInfo.Name);
        Assert.Equal("Revision A", store.Load(GosaProjectStore.BackupPath(path)).ProjectInfo.Name);
    }

    [Fact]
    public void SaveAtomic_DoesNotPromoteAnInterruptedTemporaryFile()
    {
        using var directory = new TestDirectory();
        string path = Path.Combine(directory.Path, "tower.gosa");
        var store = new GosaProjectStore();
        store.SaveAtomic(path, CreateDocument("Good"));
        File.WriteAllText(Path.Combine(directory.Path, ".tower.gosa.interrupted.tmp"), "partial bytes");

        Assert.Equal("Good", store.Load(path).ProjectInfo.Name);
    }

    [Fact]
    public void RecoverLatest_UsesNewestValidAutosaveThenFallsBackPastCorruption()
    {
        using var directory = new TestDirectory();
        string path = Path.Combine(directory.Path, "tower.gosa");
        var store = new GosaProjectStore();
        store.SaveAtomic(path, CreateDocument("Primary"));
        store.SaveAutosave(path, CreateDocument("Autosave"));

        var autosave = store.RecoverLatest(path);
        Assert.True(autosave.Recovered);
        Assert.Equal("Autosave", autosave.Document.ProjectInfo.Name);

        File.WriteAllText(GosaProjectStore.AutosavePath(path), "not a zip package");
        var fallback = store.RecoverLatest(path);
        Assert.Equal("Primary", fallback.Document.ProjectInfo.Name);
        Assert.Contains(GosaProjectStore.AutosavePath(path), fallback.RejectedPaths);
    }

    [Theory]
    [InlineData("legacy", "simple-truss.json", "trussanalyzer.truss.json/legacy")]
    [InlineData("structural-v2", "cantilever-frame.json", "trussanalyzer.structuralmodel.json/2")]
    public void LegacyMigration_ConvertsGoldenFixturesDeterministically(string folder, string fileName, string sourceFormat)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "examples", "migration", folder, fileName);
        string json = File.ReadAllText(path);
        var migration = new LegacyJsonProjectMigration();

        var first = migration.Migrate(json);
        var second = migration.Migrate(json);

        Assert.Equal(sourceFormat, first.Report.SourceFormat);
        Assert.NotEmpty(first.Document.Model.Nodes);
        Assert.NotEmpty(first.Document.Model.LineObjects);
        Assert.Equal(ProjectDocumentJson.Serialize(first.Document), ProjectDocumentJson.Serialize(second.Document));
        Assert.DoesNotContain(new Model3DValidator().Validate(first.Document), issue => issue.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Store_RejectsUnknownPackageAndWrongExtension()
    {
        using var directory = new TestDirectory();
        var store = new GosaProjectStore();
        string invalid = Path.Combine(directory.Path, "invalid.gosa");
        File.WriteAllText(invalid, "not a package");

        Assert.Throws<ProjectDocumentFormatException>(() => store.Load(invalid));
        Assert.Throws<ArgumentException>(() => store.SaveAtomic(Path.Combine(directory.Path, "invalid.json"), CreateDocument("Invalid")));
    }

    private static ProjectDocument CreateDocument(string name) => new()
    {
        ProjectInfo = new ProjectInfo { Name = name },
        AuditMetadata = new AuditMetadata
        {
            DocumentId = Guid.NewGuid(), CreatedUtc = DateTimeOffset.UnixEpoch, ModifiedUtc = DateTimeOffset.UnixEpoch,
            CreatedByVersion = "test"
        }
    };

    private sealed class TestDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GOStructAnalysisTests", Guid.NewGuid().ToString("N"));
        public TestDirectory() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
