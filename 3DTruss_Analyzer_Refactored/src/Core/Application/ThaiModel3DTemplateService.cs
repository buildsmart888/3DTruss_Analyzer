namespace TrussAnalyzer.Core.Application;

using TrussAnalyzer.Core.Domain.V1;

/// <summary>Deterministic, explicitly preliminary Thai load templates for the Model3D workspace.</summary>
public sealed class ThaiModel3DTemplateService
{
    public const string ProfileId = "TH-PRELIM-MODEL3D-2026";
    private static readonly (string Label, LoadPatternKind Kind, double SelfWeight)[] Patterns =
    [ ("DL", LoadPatternKind.Dead, 1), ("SDL", LoadPatternKind.SuperimposedDead, 0), ("LL", LoadPatternKind.Live, 0), ("RL", LoadPatternKind.RoofLive, 0),
      ("WLX+", LoadPatternKind.Wind, 0), ("WLX-", LoadPatternKind.Wind, 0), ("WLY+", LoadPatternKind.Wind, 0), ("WLY-", LoadPatternKind.Wind, 0),
      ("EQX+", LoadPatternKind.Seismic, 0), ("EQX-", LoadPatternKind.Seismic, 0), ("EQY+", LoadPatternKind.Seismic, 0), ("EQY-", LoadPatternKind.Seismic, 0) ];

    public ProjectDocument Apply(ProjectDocument document, bool replaceGenerated = false)
    {
        var patterns = document.LoadDefinitions.LoadPatterns.ToList();
        foreach (var item in Patterns)
        {
            var id = BuildingModelGenerator.StableId(ProfileId, "pattern:" + item.Label);
            if (!patterns.Any(pattern => pattern.Id == id)) patterns.Add(new LoadPattern3D { Id = id, Label = item.Label, Kind = item.Kind, SelfWeightMultiplier = item.SelfWeight, Source = Source("pattern:" + item.Label) });
        }
        var combinations = document.LoadDefinitions.LoadCombinations.ToList();
        AddCombination(combinations, patterns, "SVC-GRAVITY", "Service gravity", new() { ["DL"] = 1, ["SDL"] = 1, ["LL"] = 1 });
        AddCombination(combinations, patterns, "STR-GRAVITY", "Strength gravity", new() { ["DL"] = 1.2, ["SDL"] = 1.2, ["LL"] = 1.6, ["RL"] = .5 });
        AddCombination(combinations, patterns, "UPL-WIND-X", "Uplift wind X", new() { ["DL"] = .9, ["SDL"] = .9, ["WLX+"] = 1 });
        AddCombination(combinations, patterns, "STR-SEISMIC-X", "Strength seismic X", new() { ["DL"] = 1.2, ["SDL"] = 1.2, ["LL"] = 1, ["EQX+"] = 1 });
        var mass = document.LoadDefinitions.MassSource with { LoadPatternFactors = patterns.Where(pattern => pattern.Label is "DL" or "SDL" or "LL").ToDictionary(pattern => pattern.Id, pattern => pattern.Label == "LL" ? .25 : 1) };
        return document with { LoadDefinitions = document.LoadDefinitions with { LoadPatterns = patterns, LoadCombinations = combinations, MassSource = mass }, AuditMetadata = document.AuditMetadata with { ModifiedUtc = DateTimeOffset.UtcNow } };
    }

    private static void AddCombination(List<LoadCombination3D> combinations, List<LoadPattern3D> patterns, string key, string label, Dictionary<string, double> factors)
    {
        var id = BuildingModelGenerator.StableId(ProfileId, "combination:" + key);
        var existing = combinations.FindIndex(combination => combination.Id == id);
        if (existing >= 0) return;
        combinations.Add(new LoadCombination3D { Id = id, Label = label + " (PRELIMINARY)", LoadPatternFactors = factors.ToDictionary(factor => patterns.Single(pattern => pattern.Label == factor.Key).Id, factor => factor.Value) });
    }

    private static SourceMetadata Source(string key) => new() { SourceSystem = "ThaiModel3DTemplate", SourceVersion = ProfileId, SourceObjectId = key, Notes = "PRELIMINARY; review before qualified use." };
}
