namespace TrussAnalyzer.Core.Analysis;

using TrussAnalyzer.Core.Models;

public sealed class AnalysisResultBuilder
{
    private readonly StructuralModel _model;
    private readonly DofIndexer _dofIndexer;
    private readonly ReactionRecoveryService _reactionRecoveryService;
    private readonly ElementForceRecoveryService _elementForceRecoveryService;
    private readonly EquilibriumCheckService _equilibriumCheckService;
    private readonly SolverDiagnosticsService _solverDiagnosticsService;

    public AnalysisResultBuilder(
        StructuralModel model,
        DofIndexer dofIndexer,
        ReactionRecoveryService reactionRecoveryService,
        ElementForceRecoveryService elementForceRecoveryService,
        EquilibriumCheckService equilibriumCheckService,
        SolverDiagnosticsService solverDiagnosticsService)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _dofIndexer = dofIndexer ?? throw new ArgumentNullException(nameof(dofIndexer));
        _reactionRecoveryService = reactionRecoveryService ?? throw new ArgumentNullException(nameof(reactionRecoveryService));
        _elementForceRecoveryService = elementForceRecoveryService ?? throw new ArgumentNullException(nameof(elementForceRecoveryService));
        _equilibriumCheckService = equilibriumCheckService ?? throw new ArgumentNullException(nameof(equilibriumCheckService));
        _solverDiagnosticsService = solverDiagnosticsService ?? throw new ArgumentNullException(nameof(solverDiagnosticsService));
    }

    public List<StructuralNodeResult> BuildNodeResults(double[,] originalStiffness, double[] originalLoadVector, double[] globalDisplacements)
    {
        ArgumentNullException.ThrowIfNull(originalStiffness);
        ArgumentNullException.ThrowIfNull(originalLoadVector);
        ArgumentNullException.ThrowIfNull(globalDisplacements);

        var nodeResults = new List<StructuralNodeResult>();
        foreach (var node in _model.Nodes)
        {
            int dofBase = _dofIndexer.GetNodeDofBase(node.Id);
            var reaction = _reactionRecoveryService.RecoverNodeReaction(originalStiffness, originalLoadVector, globalDisplacements, dofBase);
            var result = new StructuralNodeResult
            {
                NodeId = node.Id,
                Displacement = new Vector3D(globalDisplacements[dofBase], globalDisplacements[dofBase + 1], globalDisplacements[dofBase + 2]),
                Rotation = new Vector3D(globalDisplacements[dofBase + 3], globalDisplacements[dofBase + 4], globalDisplacements[dofBase + 5]),
                ReactionForce = new Vector3D(node.ConstraintX ? reaction[0] : 0, node.ConstraintY ? reaction[1] : 0, node.ConstraintZ ? reaction[2] : 0),
                ReactionMoment = new Vector3D(node.ConstraintRX ? reaction[3] : 0, node.ConstraintRY ? reaction[4] : 0, node.ConstraintRZ ? reaction[5] : 0)
            };

            node.SetDisplacement(result.Displacement.X, result.Displacement.Y, result.Displacement.Z);
            node.SetRotation(result.Rotation.X, result.Rotation.Y, result.Rotation.Z);
            node.SetReactionForce(result.ReactionForce.X, result.ReactionForce.Y, result.ReactionForce.Z);
            node.SetReactionMoment(result.ReactionMoment.X, result.ReactionMoment.Y, result.ReactionMoment.Z);
            nodeResults.Add(result);
        }

        return nodeResults;
    }

    public List<ElementForceResult> BuildElementResults(
        double[] globalDisplacements,
        IReadOnlyDictionary<int, double[]> equivalentElementLoadsLocal,
        IReadOnlyDictionary<int, IReadOnlyList<MemberDiagramLoad>>? memberDiagramLoadsLocal = null)
    {
        ArgumentNullException.ThrowIfNull(globalDisplacements);
        ArgumentNullException.ThrowIfNull(equivalentElementLoadsLocal);

        return _model.Elements
            .Select(element => _elementForceRecoveryService.Recover(
                element,
                globalDisplacements,
                equivalentElementLoadsLocal,
                memberDiagramLoadsLocal))
            .ToList();
    }

    public StructuralAnalysisResult BuildAnalysisResult(
        string loadCaseName,
        List<StructuralNodeResult> nodeResults,
        List<ElementForceResult> elementResults,
        List<DesignCheckResult> designChecks,
        double[] originalLoadVector,
        int nonZeroStiffnessEntries,
        string solverName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loadCaseName);
        ArgumentNullException.ThrowIfNull(nodeResults);
        ArgumentNullException.ThrowIfNull(elementResults);
        ArgumentNullException.ThrowIfNull(designChecks);
        ArgumentNullException.ThrowIfNull(originalLoadVector);
        ArgumentException.ThrowIfNullOrWhiteSpace(solverName);

        var equilibrium = _equilibriumCheckService.Calculate(_model.Nodes, nodeResults, originalLoadVector, _dofIndexer);
        return new StructuralAnalysisResult
        {
            LoadCaseName = loadCaseName,
            NodeResults = nodeResults,
            ElementResults = elementResults,
            DesignChecks = designChecks,
            Equilibrium = equilibrium,
            MaxDisplacement = nodeResults.Count == 0 ? 0 : nodeResults.Max(result => result.Displacement.Magnitude),
            Diagnostics = _solverDiagnosticsService.Build(
                _dofIndexer,
                _model.Elements.Count,
                nonZeroStiffnessEntries,
                solverName,
                originalLoadVector,
                nodeResults,
                equilibrium)
        };
    }
}
