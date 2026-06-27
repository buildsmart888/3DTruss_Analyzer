namespace TrussAnalyzer.Core.Models;

public class StructuralAnalysisResult
{
    public string LoadCaseName { get; init; } = "Default";
    public List<StructuralNodeResult> NodeResults { get; init; } = new();
    public List<ElementForceResult> ElementResults { get; init; } = new();
    public List<DesignCheckResult> DesignChecks { get; init; } = new();
    public EquilibriumCheck Equilibrium { get; init; } = new(0, 0, 0, 1e-6);
    public double MaxDisplacement { get; init; }
    public SolverDiagnostics Diagnostics { get; init; } = new();
    public double MaxUtilization => DesignChecks.Count == 0 ? 0 : DesignChecks.Max(c => c.Utilization);
    public StructuralResultSummary Summary => new()
    {
        MaxShearY = ElementResults.Count == 0 ? 0 : ElementResults.Max(e => e.ShearY),
        MaxShearZ = ElementResults.Count == 0 ? 0 : ElementResults.Max(e => e.ShearZ),
        MaxMomentY = ElementResults.Count == 0 ? 0 : ElementResults.Max(e => e.MomentY),
        MaxMomentZ = ElementResults.Count == 0 ? 0 : ElementResults.Max(e => e.MomentZ),
        MaxTorsion = ElementResults.Count == 0 ? 0 : ElementResults.Max(e => e.Torsion),
        MaxAxialForce = ElementResults.Count == 0 ? 0 : ElementResults.Max(e => Math.Abs(e.AxialForce)),
        MaxStress = ElementResults.Count == 0 ? 0 : ElementResults.Max(e => Math.Abs(e.Stress))
    };
}

public class SolverDiagnostics
{
    public int TotalDof { get; init; }
    public int ConstrainedDof { get; init; }
    public int FreeDof => Math.Max(0, TotalDof - ConstrainedDof);
    public int ElementCount { get; init; }
    public string SolverName { get; init; } = string.Empty;
    public bool DenseSolverWarning { get; init; }
    public double MatrixDensity { get; init; }
    public double AppliedLoadMagnitude { get; init; }
    public double ReactionMagnitude { get; init; }
    public double EquilibriumResidualMagnitude { get; init; }
    public string Notes { get; init; } = string.Empty;
}

public class StructuralNodeResult
{
    public int NodeId { get; init; }
    public Vector3D Displacement { get; init; } = Vector3D.Zero;
    public Vector3D Rotation { get; init; } = Vector3D.Zero;
    public Vector3D ReactionForce { get; init; } = Vector3D.Zero;
    public Vector3D ReactionMoment { get; init; } = Vector3D.Zero;
}

public class ElementForceResult
{
    public int ElementId { get; init; }
    public double AxialForce { get; init; }
    public double ShearY { get; init; }
    public double ShearZ { get; init; }
    public double Torsion { get; init; }
    public double MomentY { get; init; }
    public double MomentZ { get; init; }
    public double Stress { get; init; }
    public double[] LocalEndForces { get; init; } = Array.Empty<double>();
    public ElementEndForceResult StartEndForces { get; init; } = new();
    public ElementEndForceResult EndEndForces { get; init; } = new();
    public List<ElementStationResult> StationResults { get; init; } = new();
}

public class ElementStationResult
{
    public int ElementId { get; init; }
    public double RelativePosition { get; init; }
    public double AxialForce { get; init; }
    public double ShearY { get; init; }
    public double ShearZ { get; init; }
    public double Torsion { get; init; }
    public double MomentY { get; init; }
    public double MomentZ { get; init; }
}

public enum DesignCheckStatus
{
    OK,
    NG,
    NotApplicable,
    MissingData
}

public class DesignCheckResult
{
    public int ElementId { get; init; }
    public string CheckType { get; init; } = string.Empty;
    public double Demand { get; init; }
    public double Capacity { get; init; }
    public double Utilization { get; init; }
    public DesignCheckStatus Status { get; init; }
    public string Notes { get; init; } = string.Empty;
}
