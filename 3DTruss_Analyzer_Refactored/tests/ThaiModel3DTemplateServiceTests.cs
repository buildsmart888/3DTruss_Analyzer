namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Application;
using TrussAnalyzer.Core.Domain.V1;
using Xunit;

public sealed class ThaiModel3DTemplateServiceTests
{
    [Fact]
    public void TemplatesAreDeterministicAndExplicitlyPreliminary()
    {
        var service = new ThaiModel3DTemplateService();
        var first = service.Apply(new ProjectDocument());
        var second = service.Apply(new ProjectDocument());

        Assert.Equal(first.LoadDefinitions.LoadPatterns.Select(item => item.Id), second.LoadDefinitions.LoadPatterns.Select(item => item.Id));
        Assert.Equal(first.LoadDefinitions.LoadCombinations.Select(item => item.Id), second.LoadDefinitions.LoadCombinations.Select(item => item.Id));
        Assert.Contains(first.LoadDefinitions.LoadPatterns, item => item.Label == "WLX+");
        Assert.Contains(first.LoadDefinitions.LoadPatterns, item => item.Label == "EQY-");
        Assert.All(first.LoadDefinitions.LoadCombinations, item => Assert.Contains("PRELIMINARY", item.Label));
        Assert.All(first.LoadDefinitions.LoadPatterns, item => Assert.Equal(ThaiModel3DTemplateService.ProfileId, item.Source.SourceVersion));
    }
}
