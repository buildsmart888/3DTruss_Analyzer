namespace TrussAnalyzer.Core.Analysis;

using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Utilities;

public sealed class LinearAnalysisRunner
{
    private readonly StructuralModel _model;
    private readonly DofIndexer _dofIndexer;
    private readonly FrameElementStiffnessProvider _stiffnessProvider;
    private readonly GlobalStiffnessAssembler _globalStiffnessAssembler;
    private readonly LoadVectorAssembler _loadVectorAssembler;
    private readonly BoundaryConditionApplier _boundaryConditionApplier;
    private readonly ILinearSystemSolver _linearSolver;
    private readonly MechanismDiagnosticsService _mechanismDiagnosticsService;
    private readonly Dictionary<int, Node> _nodes;
    private readonly Dictionary<int, Material> _materials;
    private readonly Dictionary<int, Section> _sections;

    public LinearAnalysisRunner(
        StructuralModel model,
        DofIndexer dofIndexer,
        FrameElementStiffnessProvider stiffnessProvider,
        GlobalStiffnessAssembler globalStiffnessAssembler,
        LoadVectorAssembler loadVectorAssembler,
        BoundaryConditionApplier boundaryConditionApplier,
        ILinearSystemSolver linearSolver,
        MechanismDiagnosticsService? mechanismDiagnosticsService = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _dofIndexer = dofIndexer ?? throw new ArgumentNullException(nameof(dofIndexer));
        _stiffnessProvider = stiffnessProvider ?? throw new ArgumentNullException(nameof(stiffnessProvider));
        _globalStiffnessAssembler = globalStiffnessAssembler ?? throw new ArgumentNullException(nameof(globalStiffnessAssembler));
        _loadVectorAssembler = loadVectorAssembler ?? throw new ArgumentNullException(nameof(loadVectorAssembler));
        _boundaryConditionApplier = boundaryConditionApplier ?? throw new ArgumentNullException(nameof(boundaryConditionApplier));
        _linearSolver = linearSolver ?? throw new ArgumentNullException(nameof(linearSolver));
        _mechanismDiagnosticsService = mechanismDiagnosticsService ?? new MechanismDiagnosticsService();
        _nodes = model.Nodes.ToDictionary(node => node.Id);
        _materials = model.Materials.ToDictionary(material => material.Id);
        _sections = model.Sections.ToDictionary(section => section.Id);
    }

    public LinearAnalysisRunResult RunLoadCase(string? loadCaseId = null)
    {
        var loadCase = loadCaseId == null
            ? null
            : _model.LoadCases.FirstOrDefault(loadCase => string.Equals(loadCase.CaseId, loadCaseId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Load case '{loadCaseId}' was not found.");

        return Run(loadCase?.Name ?? "Default", loadAssembly => _loadVectorAssembler.AssembleInto(loadAssembly, loadCase));
    }

    public LinearAnalysisRunResult RunCombination(string combinationId)
    {
        var combination = _model.LoadCombinations.FirstOrDefault(combination => string.Equals(combination.CombinationId, combinationId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Load combination '{combinationId}' was not found.");

        return Run(combination.Name, loadAssembly =>
        {
            foreach (var entry in combination.LoadCases)
            {
                var loadCase = _model.LoadCases.FirstOrDefault(loadCase => string.Equals(loadCase.CaseId, entry.Key, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"Load combination '{combination.Name}' references missing load case '{entry.Key}'.");
                _loadVectorAssembler.AssembleInto(loadAssembly, loadCase, entry.Value);
            }
        });
    }

    private LinearAnalysisRunResult Run(string loadCaseName, Action<LoadAssemblyResult> assembleLoads)
    {
        int totalDof = _dofIndexer.TotalDof;
        var stiffness = new double[totalDof, totalDof];
        var loadAssembly = _loadVectorAssembler.CreateResult();

        foreach (var element in _model.Elements)
            AssembleElement(stiffness, element);

        assembleLoads(loadAssembly);
        var originalStiffness = (double[,])stiffness.Clone();
        var originalLoadVector = (double[])loadAssembly.GlobalLoadVector.Clone();

        ApplyBoundaryConditions(stiffness, loadAssembly.GlobalLoadVector);
        int nonZeroStiffnessEntries = CountNonZero(stiffness);
        double[] globalDisplacements;
        try
        {
            globalDisplacements = _linearSolver.Solve(stiffness, loadAssembly.GlobalLoadVector);
        }
        catch (InvalidOperationException exception) when (MechanismDiagnosticsService.IsSingularOrUnstableFailure(exception))
        {
            var diagnostics = _mechanismDiagnosticsService.Analyze(stiffness, _model.Nodes);
            throw new StructuralInstabilityException(diagnostics, exception);
        }

        return new LinearAnalysisRunResult(
            loadCaseName,
            loadAssembly,
            originalStiffness,
            originalLoadVector,
            globalDisplacements,
            nonZeroStiffnessEntries,
            _linearSolver.Name);
    }

    private void AssembleElement(double[,] stiffness, StructuralElement element)
    {
        var start = _nodes[element.StartNodeId];
        var end = _nodes[element.EndNodeId];
        var material = _materials[element.MaterialId];
        var section = _sections[element.SectionId];
        var geometry = FrameElementGeometryResolver.Resolve(element, start, end);
        double length = geometry.Length;

        if (length < 1e-10)
            throw new InvalidOperationException($"Element {element.Id} has zero or near-zero length.");

        var localStiffness = _stiffnessProvider.BuildLocalStiffness(element, material, section, length);
        _globalStiffnessAssembler.Assemble(stiffness, localStiffness, geometry.AnalysisTransformation, _dofIndexer.GetElementDofMap(element));
    }

    private void ApplyBoundaryConditions(double[,] stiffness, double[] loadVector)
    {
        var prescribedDofValues = _model.Nodes
            .SelectMany(_dofIndexer.GetConstrainedDofValues)
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        _boundaryConditionApplier.Apply(stiffness, loadVector, prescribedDofValues);
    }

    private static int CountNonZero(double[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int columns = matrix.GetLength(1);
        int count = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (Math.Abs(matrix[row, column]) > 1e-18)
                    count++;
            }
        }

        return count;
    }
}
