namespace TrussAnalyzer.Core;

using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Utilities;

/// <summary>
/// Main solver class for 3D truss analysis using the Finite Element Method (FEM).
/// Implements direct stiffness method with proper engineering principles.
/// </summary>
public class TrussSolver
{
    private readonly List<Node> _nodes = new();
    private readonly List<Element> _elements = new();
    private readonly Dictionary<int, int> _nodeIndexMap = new();
    
    /// <summary>Global stiffness matrix</summary>
    private double[,] _globalStiffnessMatrix = new double[0, 0];
    private double[,] _unconstrainedStiffnessMatrix = new double[0, 0];
    
    /// <summary>Global force vector (units: N)</summary>
    private double[] _forceVector = Array.Empty<double>();
    private double[] _unconstrainedForceVector = Array.Empty<double>();
    
    /// <summary>Displacement solution vector (units: m)</summary>
    private double[] _displacementVector = Array.Empty<double>();

    /// <summary>Analysis results summary</summary>
    public AnalysisResult? LastResult { get; private set; }

    /// <summary>Add a node to the structure</summary>
    public void AddNode(Node node)
    {
        if (_nodeIndexMap.ContainsKey(node.Id))
            throw new InvalidOperationException($"Node with ID {node.Id} already exists.");
        
        _nodeIndexMap[node.Id] = _nodes.Count;
        _nodes.Add(node);
    }

    /// <summary>Add an element to the structure</summary>
    public void AddElement(Element element)
    {
        if (_elements.Any(e => e.Id == element.Id))
            throw new InvalidOperationException($"Element with ID {element.Id} already exists.");
        _elements.Add(element);
    }

    /// <summary>Get all nodes</summary>
    public IReadOnlyList<Node> GetNodes() => _nodes.AsReadOnly();

    /// <summary>Get all elements</summary>
    public IReadOnlyList<Element> GetElements() => _elements.AsReadOnly();

    /// <summary>
    /// Returns user-facing validation messages before analysis.
    /// </summary>
    public List<ModelValidationMessage> ValidateModel()
    {
        var messages = new List<ModelValidationMessage>();

        if (_nodes.Count == 0)
            messages.Add(new ModelValidationMessage { Severity = "Error", Message = "No nodes have been defined." });
        if (_elements.Count == 0)
            messages.Add(new ModelValidationMessage { Severity = "Error", Message = "No elements have been defined." });
        if (!_nodes.Any(n => n.IsConstrained))
            messages.Add(new ModelValidationMessage { Severity = "Error", Message = "No supports are defined." });

        int dof = _nodes.Count * 3;
        if (dof > 300)
        {
            messages.Add(new ModelValidationMessage
            {
                Severity = "Warning",
                Message = $"Model has {dof} DOF. The current dense matrix solver may be slow for large models."
            });
        }

        foreach (var element in _elements)
        {
            if (!_nodeIndexMap.ContainsKey(element.StartNodeId))
                messages.Add(new ModelValidationMessage { Severity = "Error", Message = $"Element {element.Id} references missing start node {element.StartNodeId}." });
            if (!_nodeIndexMap.ContainsKey(element.EndNodeId))
                messages.Add(new ModelValidationMessage { Severity = "Error", Message = $"Element {element.Id} references missing end node {element.EndNodeId}." });
            if (element.StartNodeId == element.EndNodeId)
                messages.Add(new ModelValidationMessage { Severity = "Error", Message = $"Element {element.Id} connects a node to itself." });
        }

        foreach (var node in _nodes.Where(n => !n.ConstraintZ && Math.Abs(n.Position.Z) < 1e-12))
        {
            bool connectedOnlyToPlanarElements = _elements
                .Where(e => e.StartNodeId == node.Id || e.EndNodeId == node.Id)
                .Where(e => _nodeIndexMap.ContainsKey(e.StartNodeId) && _nodeIndexMap.ContainsKey(e.EndNodeId))
                .All(e => GetNodeById(e.StartNodeId).Position.Z == 0 && GetNodeById(e.EndNodeId).Position.Z == 0);

            if (connectedOnlyToPlanarElements)
            {
                messages.Add(new ModelValidationMessage
                {
                    Severity = "Warning",
                    Message = $"Node {node.Id} appears to be in a 2D model but Z is unconstrained."
                });
            }
        }

        return messages;
    }

    /// <summary>
    /// Performs the complete structural analysis for a single load case.
    /// Returns: Analysis result with displacements, forces, and reactions.
    /// </summary>
    public AnalysisResult Analyze(LoadCase? loadCase = null)
    {
        ValidateStructure();
        
        int numNodes = _nodes.Count;
        int numDOF = numNodes * 3; // 3 DOF per node (X, Y, Z)

        // Initialize global matrices
        _globalStiffnessMatrix = Matrix.Create(numDOF, numDOF);
        _forceVector = new double[numDOF];

        // Step 1: Update element geometry
        UpdateElementGeometry();

        // Step 2: Assemble global stiffness matrix
        AssembleGlobalStiffnessMatrix(numDOF);

        // Step 3: Build force vector from applied loads
        BuildForceVector(numDOF, loadCase);

        _unconstrainedStiffnessMatrix = (double[,])_globalStiffnessMatrix.Clone();
        _unconstrainedForceVector = (double[])_forceVector.Clone();

        // Step 4: Apply boundary conditions
        ApplyBoundaryConditions(numDOF);

        // Step 5: Solve for displacements
        _displacementVector = Matrix.SolveAuto(_globalStiffnessMatrix, _forceVector);

        // Step 6: Extract displacements and calculate reactions
        ExtractResults(numDOF);

        // Step 7: Calculate element forces and stresses
        CalculateElementForces();

        // Step 8: Verify equilibrium
        var equilibrium = CalculateEquilibrium();
        bool equilibriumOK = equilibrium.IsSatisfied;

        LastResult = CreateResultSnapshot(loadCase?.Name ?? "Default", equilibriumOK, equilibrium);

        return LastResult;
    }

    /// <summary>
    /// Performs analysis for multiple load cases and returns all results.
    /// </summary>
    public List<AnalysisResult> AnalyzeMultipleLoadCases(List<LoadCase> loadCases)
    {
        var results = new List<AnalysisResult>();
        
        foreach (var loadCase in loadCases)
        {
            var result = Analyze(loadCase);
            results.Add(result);
        }
        
        return results;
    }

    /// <summary>
    /// Performs analysis for load combinations and returns combined results.
    /// </summary>
    public List<AnalysisResult> AnalyzeLoadCombinations(
        List<LoadCombination> combinations, 
        Dictionary<string, LoadCase> allLoadCases)
    {
        var results = new List<AnalysisResult>();
        
        foreach (var combination in combinations)
        {
            var combinedForces = CalculateCombinationForces(combination, allLoadCases);
            
            // Create a temporary load case for this combination
            var tempLoadCase = new LoadCase
            {
                CaseId = combination.CombinationId,
                Name = combination.Name,
                Type = LoadCaseType.Static,
                NodeForces = combinedForces,
                IncludeSelfWeight = false
            };
            
            var result = Analyze(tempLoadCase);
            results.Add(result);
        }
        
        return results;
    }

    /// <summary>
    /// Assembles the global stiffness matrix from element stiffness matrices.
    /// For 3D truss element, the stiffness matrix is 6x6 (2 nodes × 3 DOF).
    /// </summary>
    private void AssembleGlobalStiffnessMatrix(int numDOF)
    {
        foreach (var element in _elements)
        {
            double k = element.GetStiffnessCoefficient(); // EA/L
            var n = element.DirectionCosines; // Direction cosines [nx, ny, nz]

            // Element stiffness matrix in global coordinates (6×6)
            // k_local = k * [n⊗n] where ⊗ is outer product
            var kMatrix = new double[6, 6];
            
            // Fill the 6×6 element stiffness matrix
            // DOF order: [startX, startY, startZ, endX, endY, endZ]
            double[] nx = { n.X, n.Y, n.Z };
            
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double kij = k * nx[i] * nx[j];
                    kMatrix[i, j] = kij;           // start-start
                    kMatrix[i, j + 3] = -kij;      // start-end
                    kMatrix[i + 3, j] = -kij;      // end-start
                    kMatrix[i + 3, j + 3] = kij;   // end-end
                }
            }

            // Map element DOF to global DOF
            int startIdx = _nodeIndexMap[element.StartNodeId] * 3;
            int endIdx = _nodeIndexMap[element.EndNodeId] * 3;
            int[] dofMap = { startIdx, startIdx + 1, startIdx + 2, endIdx, endIdx + 1, endIdx + 2 };

            // Add to global matrix
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    _globalStiffnessMatrix[dofMap[i], dofMap[j]] += kMatrix[i, j];
                }
            }
        }
    }

    /// <summary>
    /// Builds the global force vector from nodal applied forces.
    /// </summary>
    private void BuildForceVector(int numDOF, LoadCase? loadCase = null)
    {
        var nodalForces = new Dictionary<int, ForceVector>();

        if (loadCase != null)
        {
            foreach (var nodeForceEntry in loadCase.NodeForces)
            {
                AddToForceDictionary(nodalForces, nodeForceEntry.Key, nodeForceEntry.Value.Multiply(loadCase.LoadFactor));
            }
        }
        else
        {
            foreach (var node in _nodes)
            {
                AddToForceDictionary(
                    nodalForces,
                    node.Id,
                    new ForceVector(node.AppliedForce.X, node.AppliedForce.Y, node.AppliedForce.Z));
            }
        }

        if (loadCase?.IncludeSelfWeight == true)
        {
            AddSelfWeightForces(nodalForces, loadCase.LoadFactor);
        }

        foreach (var node in _nodes)
        {
            if (nodalForces.TryGetValue(node.Id, out var force))
            {
                node.ApplyForce(force.Fx, force.Fy, force.Fz);
            }
            else
            {
                node.ResetForces();
            }
        }

        // Build the force vector from node applied forces
        foreach (var node in _nodes)
        {
            int idx = _nodeIndexMap[node.Id] * 3;
            _forceVector[idx] = node.AppliedForce.X;
            _forceVector[idx + 1] = node.AppliedForce.Y;
            _forceVector[idx + 2] = node.AppliedForce.Z;
        }
    }

    /// <summary>
    /// Applies boundary conditions by modifying the stiffness matrix and force vector.
    /// Uses the penalty-free approach: zero out rows/cols and set diagonal to 1.
    /// </summary>
    private void ApplyBoundaryConditions(int numDOF)
    {
        foreach (var node in _nodes)
        {
            int idx = _nodeIndexMap[node.Id] * 3;
            
            // X direction
            if (node.ConstraintX)
            {
                ZeroOutRowAndColumn(idx);
                _globalStiffnessMatrix[idx, idx] = 1.0;
                _forceVector[idx] = 0.0;
            }
            
            // Y direction
            if (node.ConstraintY)
            {
                ZeroOutRowAndColumn(idx + 1);
                _globalStiffnessMatrix[idx + 1, idx + 1] = 1.0;
                _forceVector[idx + 1] = 0.0;
            }
            
            // Z direction
            if (node.ConstraintZ)
            {
                ZeroOutRowAndColumn(idx + 2);
                _globalStiffnessMatrix[idx + 2, idx + 2] = 1.0;
                _forceVector[idx + 2] = 0.0;
            }
        }
    }

    /// <summary>
    /// Zeros out a row and column in the global stiffness matrix.
    /// </summary>
    private void ZeroOutRowAndColumn(int dofIndex)
    {
        int numDOF = _globalStiffnessMatrix.GetLength(0);
        for (int i = 0; i < numDOF; i++)
        {
            _globalStiffnessMatrix[dofIndex, i] = 0.0;
            _globalStiffnessMatrix[i, dofIndex] = 0.0;
        }
    }

    /// <summary>
    /// Extracts displacement results and calculates reaction forces.
    /// </summary>
    private void ExtractResults(int numDOF)
    {
        foreach (var node in _nodes)
        {
            int idx = _nodeIndexMap[node.Id] * 3;
            node.SetDisplacement(
                _displacementVector[idx],
                _displacementVector[idx + 1],
                _displacementVector[idx + 2]
            );

            // Calculate reaction forces: R = K × u - F_applied
            // For constrained DOFs only
            double rx = 0, ry = 0, rz = 0;
            
            if (node.ConstraintX)
            {
                rx = CalculateReaction(idx);
            }
            if (node.ConstraintY)
            {
                ry = CalculateReaction(idx + 1);
            }
            if (node.ConstraintZ)
            {
                rz = CalculateReaction(idx + 2);
            }

            node.SetReactionForce(rx, ry, rz);
        }
    }

    /// <summary>
    /// Calculates reaction force at a specific DOF.
    /// </summary>
    private double CalculateReaction(int dofIndex)
    {
        double reaction = 0;
        int numDOF = _unconstrainedStiffnessMatrix.GetLength(0);
        
        for (int j = 0; j < numDOF; j++)
        {
            reaction += _unconstrainedStiffnessMatrix[dofIndex, j] * _displacementVector[j];
        }
        
        return reaction - _unconstrainedForceVector[dofIndex];
    }

    /// <summary>
    /// Calculates axial forces and stresses in all elements.
    /// </summary>
    private void CalculateElementForces()
    {
        foreach (var element in _elements)
        {
            var startNode = GetNodeById(element.StartNodeId);
            var endNode = GetNodeById(element.EndNodeId);
            
            element.UpdateForces(startNode.Displacement, endNode.Displacement);
        }
    }

    /// <summary>
    /// Verifies that the structure is in equilibrium.
    /// Sum of (Applied Forces + Reactions) should equal zero.
    /// </summary>
    private EquilibriumCheck CalculateEquilibrium()
    {
        double sumFx = 0, sumFy = 0, sumFz = 0;
        double loadScale = 0;
        
        foreach (var node in _nodes)
        {
            sumFx += node.AppliedForce.X + node.ReactionForce.X;
            sumFy += node.AppliedForce.Y + node.ReactionForce.Y;
            sumFz += node.AppliedForce.Z + node.ReactionForce.Z;
            loadScale += node.AppliedForce.Magnitude + node.ReactionForce.Magnitude;
        }

        double tolerance = Math.Max(1e-6, loadScale * 1e-9);
        return new EquilibriumCheck(sumFx, sumFy, sumFz, tolerance);
    }

    /// <summary>
    /// Validates the structure before analysis.
    /// </summary>
    private void ValidateStructure()
    {
        if (_nodes.Count < 2)
            throw new InvalidOperationException("Structure must have at least 2 nodes.");
        
        if (_elements.Count < 1)
            throw new InvalidOperationException("Structure must have at least 1 element.");

        // Check that all element nodes exist
        foreach (var element in _elements)
        {
            if (!_nodeIndexMap.ContainsKey(element.StartNodeId))
                throw new InvalidOperationException($"Element {element.Id} references non-existent start node {element.StartNodeId}.");
            if (!_nodeIndexMap.ContainsKey(element.EndNodeId))
                throw new InvalidOperationException($"Element {element.Id} references non-existent end node {element.EndNodeId}.");
        }

        bool hasAnySupport = _nodes.Any(n => n.IsConstrained);
        if (!hasAnySupport)
            throw new InvalidOperationException("Structure has no supports. At least one constrained degree of freedom is required.");
    }

    /// <summary>
    /// Gets a node by its ID.
    /// </summary>
    private Node GetNodeById(int id)
    {
        if (!_nodeIndexMap.TryGetValue(id, out int index))
            throw new InvalidOperationException($"Node with ID {id} not found.");
        return _nodes[index];
    }

    private Dictionary<int, ForceVector> CalculateCombinationForces(
        LoadCombination combination,
        Dictionary<string, LoadCase> allLoadCases)
    {
        UpdateElementGeometry();
        var combinedForces = new Dictionary<int, ForceVector>();

        foreach (var loadCaseEntry in combination.LoadCases)
        {
            string caseId = loadCaseEntry.Key;
            double factor = loadCaseEntry.Value;

            if (!allLoadCases.TryGetValue(caseId, out var loadCase))
                throw new InvalidOperationException($"Load combination '{combination.Name}' references missing load case '{caseId}'.");

            double totalFactor = factor * loadCase.LoadFactor;

            foreach (var nodeForceEntry in loadCase.NodeForces)
            {
                AddToForceDictionary(combinedForces, nodeForceEntry.Key, nodeForceEntry.Value.Multiply(totalFactor));
            }

            if (loadCase.IncludeSelfWeight)
            {
                AddSelfWeightForces(combinedForces, totalFactor);
            }
        }

        return combinedForces;
    }

    private void UpdateElementGeometry()
    {
        foreach (var element in _elements)
        {
            var startNode = GetNodeById(element.StartNodeId);
            var endNode = GetNodeById(element.EndNodeId);
            element.UpdateGeometry(startNode.Position, endNode.Position);
        }
    }

    private void AddSelfWeightForces(Dictionary<int, ForceVector> nodalForces, double factor)
    {
        foreach (var element in _elements)
        {
            var selfWeight = new ForceVector(0, 0, -element.SelfWeightPerNode * factor);
            AddToForceDictionary(nodalForces, element.StartNodeId, selfWeight);
            AddToForceDictionary(nodalForces, element.EndNodeId, selfWeight);
        }
    }

    private static void AddToForceDictionary(Dictionary<int, ForceVector> forces, int nodeId, ForceVector force)
    {
        if (forces.TryGetValue(nodeId, out var current))
        {
            forces[nodeId] = current + force;
        }
        else
        {
            forces[nodeId] = force;
        }
    }

    private AnalysisResult CreateResultSnapshot(string loadCaseName, bool equilibriumOK, EquilibriumCheck equilibrium)
    {
        var nodeSnapshots = _nodes.Select(n =>
        {
            var node = new Node(n.Id, n.Position)
            {
                ConstraintX = n.ConstraintX,
                ConstraintY = n.ConstraintY,
                ConstraintZ = n.ConstraintZ
            };
            node.ApplyForce(n.AppliedForce.X, n.AppliedForce.Y, n.AppliedForce.Z);
            node.SetDisplacement(n.Displacement.X, n.Displacement.Y, n.Displacement.Z);
            node.SetReactionForce(n.ReactionForce.X, n.ReactionForce.Y, n.ReactionForce.Z);
            return node;
        }).ToList();

        var elementSnapshots = _elements.Select(e =>
        {
            var element = new Element(e.Id, e.StartNodeId, e.EndNodeId, e.Area, e.Material);
            var startNode = GetNodeById(e.StartNodeId);
            var endNode = GetNodeById(e.EndNodeId);
            element.UpdateGeometry(startNode.Position, endNode.Position);
            element.AxialForce = e.AxialForce;
            element.Stress = e.Stress;
            element.Strain = e.Strain;
            return element;
        }).ToList();

        return new AnalysisResult
        {
            Nodes = nodeSnapshots,
            Elements = elementSnapshots,
            EquilibriumSatisfied = equilibriumOK,
            Equilibrium = equilibrium,
            SafetyChecks = CalculateSafetyChecks(elementSnapshots),
            MaxDisplacement = nodeSnapshots.Max(n => n.Displacement.Magnitude),
            MaxAxialForce = elementSnapshots.Max(e => Math.Abs(e.AxialForce)),
            MaxStress = elementSnapshots.Max(e => Math.Abs(e.Stress)),
            LoadCaseName = loadCaseName
        };
    }

    private static SafetyCheckSummary CalculateSafetyChecks(List<Element> elements)
    {
        var checks = elements.Select(e =>
        {
            double allowable = e.Material.YieldStrength;
            double demand = Math.Abs(e.Stress);
            double utilization = allowable > 0 ? demand / allowable : 0;
            bool pass = allowable > 0 && utilization <= 1.0;

            return new ElementSafetyCheck
            {
                ElementId = e.Id,
                DemandStress = demand,
                AllowableStress = allowable,
                UtilizationRatio = utilization,
                IsPassing = pass,
                Status = allowable <= 0
                    ? "No yield strength"
                    : pass ? "OK" : "NG"
            };
        }).ToList();

        return new SafetyCheckSummary { ElementChecks = checks };
    }
}

/// <summary>
/// Contains the results of a structural analysis.
/// </summary>
public class AnalysisResult
{
    public List<Node> Nodes { get; init; } = new();
    public List<Element> Elements { get; init; } = new();
    public bool EquilibriumSatisfied { get; init; }
    public EquilibriumCheck Equilibrium { get; init; } = new(0, 0, 0, 1e-6);
    public SafetyCheckSummary SafetyChecks { get; init; } = new();
    public double MaxDisplacement { get; init; }
    public double MaxAxialForce { get; init; }
    public double MaxStress { get; init; }
    
    /// <summary>
    /// Name of the load case used for this analysis
    /// </summary>
    public string LoadCaseName { get; init; } = "Default";
    
    /// <summary>
    /// Total applied load magnitude (sum of all nodal forces)
    /// </summary>
    public double TotalAppliedLoad => Nodes.Sum(n => n.AppliedForce.Magnitude);
    
    /// <summary>
    /// Total reaction force magnitude (sum of all reaction forces)
    /// </summary>
    public double TotalReactionForce => Nodes.Sum(n => n.ReactionForce.Magnitude);

    public override string ToString()
    {
        return $"Analysis Result ({LoadCaseName}):\n" +
               $"  Equilibrium: {(EquilibriumSatisfied ? "✓ Satisfied" : "✗ NOT Satisfied")}\n" +
               $"  Max Displacement: {MaxDisplacement:E4} m\n" +
               $"  Max Axial Force: {MaxAxialForce:E2} N\n" +
               $"  Max Stress: {MaxStress:E2} Pa\n" +
               $"  Total Applied Load: {TotalAppliedLoad:F2} N\n" +
               $"  Total Reaction Force: {TotalReactionForce:F2} N";
    }
}

/// <summary>
/// Residual force check for global static equilibrium.
/// </summary>
public class EquilibriumCheck
{
    public double SumFX { get; }
    public double SumFY { get; }
    public double SumFZ { get; }
    public double Tolerance { get; }
    public double ResidualMagnitude => Math.Sqrt(SumFX * SumFX + SumFY * SumFY + SumFZ * SumFZ);
    public bool IsSatisfied =>
        Math.Abs(SumFX) <= Tolerance &&
        Math.Abs(SumFY) <= Tolerance &&
        Math.Abs(SumFZ) <= Tolerance;

    public EquilibriumCheck(double sumFX, double sumFY, double sumFZ, double tolerance)
    {
        SumFX = sumFX;
        SumFY = sumFY;
        SumFZ = sumFZ;
        Tolerance = tolerance;
    }
}
