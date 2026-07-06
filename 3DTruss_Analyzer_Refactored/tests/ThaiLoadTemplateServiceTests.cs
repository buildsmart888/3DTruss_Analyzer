namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Design.ThaiCode;
using TrussAnalyzer.Core.Models;
using Xunit;

public class ThaiLoadTemplateServiceTests
{
    [Fact]
    public void GenerateDefaultTemplates_CreatesStandardThaiLoadCases()
    {
        var templates = new ThaiLoadTemplateService().GenerateDefaultTemplates();
        var caseIds = templates.LoadCases.Select(lc => lc.CaseId).ToList();

        Assert.Equal(12, templates.LoadCases.Count);
        Assert.Contains("DL", caseIds);
        Assert.Contains("SDL", caseIds);
        Assert.Contains("LL", caseIds);
        Assert.Contains("RL", caseIds);
        Assert.Contains("WLX+", caseIds);
        Assert.Contains("WLX-", caseIds);
        Assert.Contains("WLY+", caseIds);
        Assert.Contains("WLY-", caseIds);
        Assert.Contains("EQX+", caseIds);
        Assert.Contains("EQX-", caseIds);
        Assert.Contains("EQY+", caseIds);
        Assert.Contains("EQY-", caseIds);
        Assert.True(templates.LoadCases.Single(lc => lc.CaseId == "DL").IncludeSelfWeight);
        Assert.Equal(LoadCaseType.Wind, templates.LoadCases.Single(lc => lc.CaseId == "WLX+").Type);
        Assert.Equal(LoadCaseType.Seismic, templates.LoadCases.Single(lc => lc.CaseId == "EQY-").Type);
        Assert.All(templates.LoadCases, lc => Assert.Contains(templates.Profile.ProfileId, lc.Description));
    }

    [Fact]
    public void GenerateDefaultTemplates_CreatesPredictablePreliminaryCombinations()
    {
        var templates = new ThaiLoadTemplateService().GenerateDefaultTemplates();
        var strengthGravity = templates.LoadCombinations.Single(c => c.CombinationId == "STR-DL");
        var windStrength = templates.LoadCombinations.Single(c => c.CombinationId == "STR-WLX+");
        var seismicUplift = templates.LoadCombinations.Single(c => c.CombinationId == "UPL-EQY-");

        Assert.Equal(1.2, strengthGravity.LoadCases["DL"], precision: 10);
        Assert.Equal(1.2, strengthGravity.LoadCases["SDL"], precision: 10);
        Assert.Equal(1.6, strengthGravity.LoadCases["LL"], precision: 10);
        Assert.Equal(0.5, strengthGravity.LoadCases["RL"], precision: 10);

        Assert.Equal(1.0, windStrength.LoadCases["WLX+"], precision: 10);
        Assert.Equal(1.0, seismicUplift.LoadCases["EQY-"], precision: 10);
        Assert.Equal(0.9, seismicUplift.LoadCases["DL"], precision: 10);
        Assert.All(templates.LoadCombinations, c => Assert.Contains("preliminary", c.Description, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GeneratedCombinations_ReferenceGeneratedLoadCases()
    {
        var templates = new ThaiLoadTemplateService().GenerateDefaultTemplates();
        var caseIds = templates.LoadCases.Select(lc => lc.CaseId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(templates.LoadCombinations);
        foreach (var combination in templates.LoadCombinations)
        {
            Assert.NotEmpty(combination.LoadCases);
            Assert.All(combination.LoadCases.Keys, caseId => Assert.Contains(caseId, caseIds));
        }
    }

    [Fact]
    public void ApplyDefaultTemplates_IsIdempotentAndPreservesExistingLoadCases()
    {
        var model = new StructuralModel();
        model.LoadCases.Add(new LoadCase
        {
            CaseId = "DL",
            Name = "Project Dead Load",
            IncludeSelfWeight = false,
            Description = "Existing project-owned load case."
        });

        var service = new ThaiLoadTemplateService();
        service.ApplyDefaultTemplates(model);
        service.ApplyDefaultTemplates(model);

        Assert.Equal(12, model.LoadCases.Count);
        Assert.Equal(30, model.LoadCombinations.Count);
        Assert.Equal("Project Dead Load", model.LoadCases.Single(lc => lc.CaseId == "DL").Name);
        Assert.False(model.LoadCases.Single(lc => lc.CaseId == "DL").IncludeSelfWeight);
        Assert.Single(model.LoadCases.Where(lc => lc.CaseId == "DL"));
        Assert.Single(model.LoadCombinations.Where(c => c.CombinationId == "STR-DL"));
    }
}

