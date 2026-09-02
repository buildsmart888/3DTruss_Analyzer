using TrussAnalyzer.Core.IO.Projects;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: ProjectMigration <legacy-or-v2.json> <output.gosa>");
    return 2;
}

string sourcePath = Path.GetFullPath(args[0]);
string targetPath = Path.GetFullPath(args[1]);
if (!File.Exists(sourcePath))
{
    Console.Error.WriteLine($"Source file was not found: {sourcePath}");
    return 2;
}

try
{
    var migration = new LegacyJsonProjectMigration().Migrate(File.ReadAllText(sourcePath));
    foreach (var entry in migration.Report.Entries)
        Console.WriteLine($"[{entry.Severity}] {entry.Code}: {entry.Message}");
    if (migration.Report.HasErrors)
    {
        Console.Error.WriteLine("Migration has errors; no output package was written.");
        return 1;
    }

    new GosaProjectStore().SaveAtomic(targetPath, migration.Document);
    Console.WriteLine($"Created {targetPath}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Migration failed: {exception.Message}");
    return 1;
}
