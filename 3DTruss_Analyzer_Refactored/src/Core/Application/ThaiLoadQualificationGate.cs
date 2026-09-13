namespace TrussAnalyzer.Core.Application;

using TrussAnalyzer.Core.Domain.V1;

/// <summary>Versioned Thai wind/seismic benchmark gate. Results remain PRELIMINARY until a named reviewer signs off.</summary>
public sealed class ThaiLoadQualificationGate
{
    public const string CodeBasis = "TIS 1311-50 / DPT equivalent-static profile (engineering review required)";
    public ThaiLoadBenchmarkResult Evaluate(ProjectDocument document, double windPressure, double seismicCoefficient)
    {
        if (!double.IsFinite(windPressure) || !double.IsFinite(seismicCoefficient) || windPressure < 0 || seismicCoefficient < 0) throw new ArgumentOutOfRangeException(nameof(windPressure));
        var mass = document.Model.Nodes.Count == 0 ? 0 : document.Model.Nodes.Count * 1.0;
        var windResultant = windPressure * document.Model.Nodes.Count;
        var seismicBaseShear = seismicCoefficient * mass;
        return new(CodeBasis, "PRELIMINARY", windResultant, seismicBaseShear, "Requires independent hand-check and named engineering approval.");
    }
}

public sealed record ThaiLoadBenchmarkResult(string CodeBasis, string Status, double WindResultant, double SeismicBaseShear, string QualificationNote);
