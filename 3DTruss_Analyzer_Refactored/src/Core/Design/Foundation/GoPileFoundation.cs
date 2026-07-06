namespace TrussAnalyzer.Core.Design.Foundation;

using TrussAnalyzer.Core.Models;

public enum GoPileFoundationType
{
    F1 = 1,
    F2 = 2,
    F3 = 3,
    F4 = 4,
    F5 = 5
}

public sealed class GoPileInput
{
    public GoPileFoundationType FoundationType { get; init; } = GoPileFoundationType.F4;
    public double AxialCompression { get; init; }
    public double MomentX { get; init; }
    public double MomentY { get; init; }
    public double ColumnOffsetX { get; init; }
    public double ColumnOffsetY { get; init; }
    public double PileSpacingX { get; init; } = 1.5;
    public double PileSpacingY { get; init; } = 1.5;
    public double PileCapacityCompression { get; init; } = 350_000;
    public double PileCapacityTension { get; init; }
    public double FootingLengthX { get; init; } = 2.4;
    public double FootingWidthY { get; init; } = 2.4;
    public double FootingThickness { get; init; } = 0.6;
    public double ColumnSizeX { get; init; } = 0.3;
    public double ColumnSizeY { get; init; } = 0.3;
    public double ConcreteCompressiveStrength { get; init; } = 24e6;
    public double RebarYieldStrength { get; init; } = 390e6;
    public double ConcreteCover { get; init; } = 0.075;
    public double BarDiameter { get; init; } = 0.016;
    public double StrengthReductionFactor { get; init; } = 0.9;
}

public sealed class GoPileResult
{
    public GoPileInput Input { get; init; } = new();
    public IReadOnlyList<PileReactionResult> Piles { get; init; } = Array.Empty<PileReactionResult>();
    public Point3D PileCentroid { get; init; }
    public double TotalCompression { get; init; }
    public double AppliedMomentX { get; init; }
    public double AppliedMomentY { get; init; }
    public double MaxCompression { get; init; }
    public double MaxTension { get; init; }
    public bool PileCapacityPass { get; init; }
    public bool UpliftPass { get; init; }
    public ReinforcementDesignResult ReinforcementX { get; init; } = new();
    public ReinforcementDesignResult ReinforcementY { get; init; } = new();
    public bool OverallPass => PileCapacityPass && UpliftPass && ReinforcementX.IsPassing && ReinforcementY.IsPassing;
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed class PileReactionResult
{
    public int Id { get; init; }
    public Point3D Position { get; init; }
    public double Reaction { get; init; }
    public double CompressionUtilization { get; init; }
    public double TensionUtilization { get; init; }
    public bool CompressionPass { get; init; }
    public bool TensionPass { get; init; }
}

public sealed class ReinforcementDesignResult
{
    public string Direction { get; init; } = string.Empty;
    public double DesignMoment { get; init; }
    public double EffectiveDepth { get; init; }
    public double RequiredSteelAreaPerMeter { get; init; }
    public double ProvidedSteelAreaPerMeter { get; init; }
    public double BarDiameter { get; init; }
    public double BarSpacing { get; init; }
    public bool IsPassing { get; init; }
}

