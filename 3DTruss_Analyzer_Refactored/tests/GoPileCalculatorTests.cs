namespace TrussAnalyzer.Tests;

using TrussAnalyzer.Core.Design.Foundation;
using Xunit;

public class GoPileCalculatorTests
{
    [Fact]
    public void F4ConcentricLoad_DistributesEqually()
    {
        var result = new GoPileCalculator().Calculate(new GoPileInput
        {
            FoundationType = GoPileFoundationType.F4,
            AxialCompression = 800_000,
            PileCapacityCompression = 300_000
        });

        Assert.Equal(4, result.Piles.Count);
        Assert.All(result.Piles, pile => Assert.Equal(200_000, pile.Reaction, precision: 6));
        Assert.True(result.PileCapacityPass);
        Assert.True(result.OverallPass);
    }

    [Fact]
    public void F2OffsetColumn_RecalculatesPileReactionsFromEccentricity()
    {
        var result = new GoPileCalculator().Calculate(new GoPileInput
        {
            FoundationType = GoPileFoundationType.F2,
            AxialCompression = 1_000_000,
            ColumnOffsetX = 0.15,
            PileSpacingX = 1.5,
            PileCapacityCompression = 800_000
        });

        Assert.Equal(600_000, result.Piles.Single(p => p.Position.X > 0).Reaction, precision: 6);
        Assert.Equal(400_000, result.Piles.Single(p => p.Position.X < 0).Reaction, precision: 6);
        Assert.Equal(1_000_000, result.TotalCompression, precision: 6);
    }

    [Fact]
    public void LargeMoment_FlagsCompressionOverloadAndUplift()
    {
        var result = new GoPileCalculator().Calculate(new GoPileInput
        {
            FoundationType = GoPileFoundationType.F2,
            AxialCompression = 100_000,
            MomentY = -300_000,
            PileSpacingX = 1.0,
            PileCapacityCompression = 300_000,
            PileCapacityTension = 0
        });

        Assert.False(result.PileCapacityPass);
        Assert.False(result.UpliftPass);
        Assert.Contains(result.Piles, p => p.Reaction < 0);
        Assert.Contains(result.Piles, p => p.Reaction > 300_000);
    }

    [Fact]
    public void F5Layout_IncludesCenterPile()
    {
        var layout = new GoPileCalculator().CreatePileLayout(new GoPileInput
        {
            FoundationType = GoPileFoundationType.F5
        });

        Assert.Equal(5, layout.Count);
        Assert.Contains(layout, p => Math.Abs(p.X) < 1e-12 && Math.Abs(p.Y) < 1e-12);
    }
}
