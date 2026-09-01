namespace TrussAnalyzer.Core.Models;

public enum FrameBendingFormulation
{
    EulerBernoulli,
    Timoshenko
}

public sealed class FrameAnalysisOptions
{
    public FrameBendingFormulation BendingFormulation { get; set; } = FrameBendingFormulation.EulerBernoulli;
    public double ShearCorrectionFactorY { get; set; } = 5.0 / 6.0;
    public double ShearCorrectionFactorZ { get; set; } = 5.0 / 6.0;

    public bool UsesTimoshenkoShearDeformation => BendingFormulation == FrameBendingFormulation.Timoshenko;
}
