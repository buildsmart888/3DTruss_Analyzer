namespace TrussAnalyzer.Core;

using TrussAnalyzer.Core.Analysis;
using TrussAnalyzer.Core.Design;
using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Analysis.Validation;
using TrussAnalyzer.Core.Utilities;

public class StructuralSolver
{
    private readonly StructuralModel _model;
    private readonly LinearAnalysisRunner _linearAnalysisRunner;
    private readonly AnalysisResultBuilder _analysisResultBuilder;
    private readonly DesignCheckRunner _designCheckRunner;

    public StructuralSolver(StructuralModel model, ILinearSystemSolver? linearSolver = null)
    {
        _model = model;
        var dofIndexer = new DofIndexer(model.Nodes);
        var stiffnessProvider = new FrameElementStiffnessProvider(model.FrameAnalysisOptions);
        var activeLinearSolver = linearSolver ?? new DenseLinearSystemSolver();
        var loadVectorAssembler = new LoadVectorAssembler(model, dofIndexer, stiffnessProvider);
        var elementForceRecoveryService = new ElementForceRecoveryService(model, dofIndexer, stiffnessProvider);
        _linearAnalysisRunner = new LinearAnalysisRunner(
            model,
            dofIndexer,
            stiffnessProvider,
            new GlobalStiffnessAssembler(),
            loadVectorAssembler,
            new BoundaryConditionApplier(),
            activeLinearSolver);
        _analysisResultBuilder = new AnalysisResultBuilder(
            model,
            dofIndexer,
            new ReactionRecoveryService(),
            elementForceRecoveryService,
            new EquilibriumCheckService(),
            new SolverDiagnosticsService());
        _designCheckRunner = new DesignCheckRunner(model);
    }

    public IReadOnlyList<ModelValidationMessage> ValidateModel()
    {
        return new ModelValidator(_model).Validate();
    }

    public StructuralAnalysisResult Analyze(string? loadCaseId = null)
    {
        var errors = ValidateModel().Where(m => m.Severity == "Error").ToList();
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(e => e.Message)));

        return BuildResult(_linearAnalysisRunner.RunLoadCase(loadCaseId));
    }

    public StructuralAnalysisResult AnalyzeCombination(string combinationId)
    {
        return BuildResult(_linearAnalysisRunner.RunCombination(combinationId));
    }

    public static LocalAxes GetLocalAxes(Point3D start, Point3D end, double rollAngleRadians = 0)
    {
        return FrameCoordinateSystem.GetLocalAxes(start, end, rollAngleRadians);
    }

    public static double[,] BuildTransformation(Point3D start, Point3D end, double rollAngleRadians = 0)
    {
        return FrameCoordinateSystem.BuildTransformation(start, end, rollAngleRadians);
    }

    private StructuralAnalysisResult BuildResult(LinearAnalysisRunResult analysisRun)
    {
        var nodeResults = _analysisResultBuilder.BuildNodeResults(
            analysisRun.OriginalStiffness,
            analysisRun.OriginalLoadVector,
            analysisRun.GlobalDisplacements);
        var elementResults = _analysisResultBuilder.BuildElementResults(
            analysisRun.GlobalDisplacements,
            analysisRun.LoadAssembly.EquivalentElementLoadsLocal,
            analysisRun.LoadAssembly.MemberDiagramLoadsLocal);
        var checks = _designCheckRunner.Run(elementResults);
        return _analysisResultBuilder.BuildAnalysisResult(
            analysisRun.LoadCaseName,
            nodeResults,
            elementResults,
            checks,
            analysisRun.OriginalLoadVector,
            analysisRun.NonZeroStiffnessEntries,
            analysisRun.SolverName);
    }

}
