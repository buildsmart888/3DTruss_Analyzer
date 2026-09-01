namespace TrussAnalyzer.Core.Analysis;

public sealed class LinearAnalysisRunResult
{
    public LinearAnalysisRunResult(
        string loadCaseName,
        LoadAssemblyResult loadAssembly,
        double[,] originalStiffness,
        double[] originalLoadVector,
        double[] globalDisplacements,
        int nonZeroStiffnessEntries,
        string solverName)
    {
        LoadCaseName = loadCaseName;
        LoadAssembly = loadAssembly;
        OriginalStiffness = originalStiffness;
        OriginalLoadVector = originalLoadVector;
        GlobalDisplacements = globalDisplacements;
        NonZeroStiffnessEntries = nonZeroStiffnessEntries;
        SolverName = solverName;
    }

    public string LoadCaseName { get; }
    public LoadAssemblyResult LoadAssembly { get; }
    public double[,] OriginalStiffness { get; }
    public double[] OriginalLoadVector { get; }
    public double[] GlobalDisplacements { get; }
    public int NonZeroStiffnessEntries { get; }
    public string SolverName { get; }
}
