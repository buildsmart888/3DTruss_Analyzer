namespace TrussAnalyzer.Core.Models;

public class DesignSettings
{
    public double SteelResistanceFactor { get; init; } = 0.9;
    public double ConcreteFlexureResistanceFactor { get; init; } = 0.9;
    public double ConcreteShearResistanceFactor { get; init; } = 0.75;
    public double CompressionEffectiveLengthFactor { get; init; } = 1.0;
    public double DefaultSteelYieldStrength { get; init; } = 250e6;
    public double DefaultRebarYieldStrength { get; init; } = 420e6;
    public string SectionClassification { get; init; } = "MVP compact placeholder";
}
