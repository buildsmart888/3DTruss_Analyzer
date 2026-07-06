namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Visualization;
using Xunit;

public class SectionVisualProfileServiceTests
{
    [Fact]
    public void RectangularSection_CreatesSingleRectangleProfile()
    {
        var profile = SectionVisualProfileService.Create(Section.Rectangular(1, "B300x500", 0.3, 0.5));

        var rectangle = Assert.Single(profile.Rectangles);
        Assert.Equal(0.3, rectangle.WidthY, precision: 10);
        Assert.Equal(0.5, rectangle.DepthZ, precision: 10);
        Assert.False(profile.IsCircular);
    }

    [Fact]
    public void IShape_CreatesTwoFlangesAndWeb()
    {
        var profile = SectionVisualProfileService.Create(new Section
        {
            Id = 1,
            Type = SectionType.IShape,
            Width = 0.2,
            Depth = 0.4,
            Thickness = 0.012
        });

        Assert.Equal(3, profile.Rectangles.Count);
        Assert.Contains(profile.Rectangles, r => Math.Abs(r.WidthY - 0.2) < 1e-12 && Math.Abs(r.DepthZ - 0.012) < 1e-12);
        Assert.Contains(profile.Rectangles, r => Math.Abs(r.WidthY - 0.012) < 1e-12 && r.DepthZ > 0.35);
    }

    [Fact]
    public void ChannelSection_CreatesCShapeProfile()
    {
        var profile = SectionVisualProfileService.Create(new Section
        {
            Id = 1,
            Type = SectionType.Channel,
            Width = 0.15,
            Depth = 0.3,
            Thickness = 0.01
        });

        Assert.Equal(3, profile.Rectangles.Count);
        Assert.Contains(profile.Rectangles, r => r.CenterY < 0 && Math.Abs(r.WidthY - 0.01) < 1e-12);
        Assert.Contains("Channel", profile.Notes);
    }

    [Fact]
    public void PipeSection_UsesOuterDiameter()
    {
        var profile = SectionVisualProfileService.Create(new Section
        {
            Id = 1,
            Type = SectionType.Pipe,
            Diameter = 0.114,
            Thickness = 0.006
        });

        Assert.True(profile.IsCircular);
        Assert.Empty(profile.Rectangles);
        Assert.Equal(0.114, profile.Diameter, precision: 10);
        Assert.Contains("outside diameter", profile.Notes);
    }

    [Fact]
    public void GenericSection_WithArea_CreatesEquivalentSquare()
    {
        var profile = SectionVisualProfileService.Create(Section.Generic(1, "Generic", 0.04, 1e-4, 1e-4, 1e-4));

        var rectangle = Assert.Single(profile.Rectangles);
        Assert.Equal(0.2, rectangle.WidthY, precision: 10);
        Assert.Equal(0.2, rectangle.DepthZ, precision: 10);
    }
}
