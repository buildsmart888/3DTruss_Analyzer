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
    private double[,] _globalStiffnessMatrix = Array.Empty<double[]>();
    
    /// <summary>Global force vector (units: N)</summary>
    private double[] _forceVector = Array.Empty<double>();
    
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
        _elements.Add(element);
    }

    /// <summary>Get all nodes</summary>
    public IReadOnlyList<Node> GetNodes() => _nodes.AsReadOnly();

    /// <summary>Get all elements</summary>
    public IReadOnlyList<Element> GetElements() => _elements.AsReadOnly();

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

        // Reset all node forces before applying new loads
        foreach (var node in _nodes)
        {
            node.ResetForces();
        }

        // Step 1: Update element geometry and apply self-weight if needed
        foreach (var element in _elements)
        {
            var startNode = GetNodeById(element.StartNodeId);
            var endNode = GetNodeById(element.EndNodeId);
            
            element.UpdateGeometry(startNode.Position, endNode.Position);
            
            // Apply self-weight to nodes (correctly: half to each node)
            // Only if no specific load case or if load case includes self-weight
            if (loadCase == null || loadCase.IncludeSelfWeight)
            {
                startNode.AddForce(0, 0, -element.SelfWeightPerNode);
                endNode.AddForce(0, 0, -element.SelfWeightPerNode);
            }
        }

        // Step 2: Assemble global stiffness matrix
        AssembleGlobalStiffnessMatrix(numDOF);

        // Step 3: Build force vector from applied loads
        BuildForceVector(numDOF, loadCase);

        // Step 4: Apply boundary conditions
        ApplyBoundaryConditions(numDOF);

        // Step 5: Solve for displacements
        _displacementVector = Matrix.Solve(_globalStiffnessMatrix, _forceVector);

        // Step 6: Extract displacements and calculate reactions
        ExtractResults(numDOF);

        // Step 7: Calculate element forces and stresses
        CalculateElementForces();

        // Step 8: Verify equilibrium
        bool equilibriumOK = VerifyEquilibrium();

        LastResult = new AnalysisResult
        {
            Nodes = _nodes.ToList(),
            Elements = _elements.ToList(),
            EquilibriumSatisfied = equilibriumOK,
            MaxDisplacement = _nodes.Max(n => n.Displacement.Magnitude),
            MaxAxialForce = _elements.Max(e => Math.Abs(e.AxialForce)),
            MaxStress = _elements.Max(e => Math.Abs(e.Stress)),
            LoadCaseName = loadCase?.Name ?? "Default"
        };

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
            // Calculate combined forces
            var combinedForces = combination.CalculateCombinedForces(allLoadCases);
            
            // Create a temporary load case for this combination
            var tempLoadCase = new LoadCase
            {
                CaseId = combination.CombinationId,
                Name = combination.Name,
                Type = LoadCaseType.Static,
                NodeForces = combinedForces,
                IncludeSelfWeight = false // Self-weight already included in individual load cases
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
        // Reset all forces first
        foreach (var node in _nodes)
        {
            node.ResetForces();
        }

        // If a specific load case is provided, apply forces from that load case
        if (loadCase != null)
        {
            foreach (var nodeForceEntry in loadCase.NodeForces)
            {
                int nodeId = nodeForceEntry.Key;
                var force = nodeForceEntry.Value;
                
                var node = GetNodeById(nodeId);
                node.AddForce(force.Fx, force.Fy, force.Fz);
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
                rx = CalculateReaction(idx) - node.AppliedForce.X;
            }
            if (node.ConstraintY)
            {
                ry = CalculateReaction(idx + 1) - node.AppliedForce.Y;
            }
            if (node.ConstraintZ)
            {
                rz = CalculateReaction(idx + 2) - node.AppliedForce.Z;
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
        int numDOF = _globalStiffnessMatrix.GetLength(0);
        
        for (int j = 0; j < numDOF; j++)
        {
            reaction += _globalStiffnessMatrix[dofIndex, j] * _displacementVector[j];
        }
        
        return reaction;
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
    private bool VerifyEquilibrium()
    {
        double sumFx = 0, sumFy = 0, sumFz = 0;
        
        foreach (var node in _nodes)
        {
            sumFx += node.AppliedForce.X + node.ReactionForce.X;
            sumFy += node.AppliedForce.Y + node.ReactionForce.Y;
            sumFz += node.AppliedForce.Z + node.ReactionForce.Z;
        }

        double tolerance = 1e-6;
        bool ok = Math.Abs(sumFx) < tolerance && 
                  Math.Abs(sumFy) < tolerance && 
                  Math.Abs(sumFz) < tolerance;

        return ok;
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

        // Check for adequate supports
        int totalConstraints = _nodes.Sum(n => 
            (n.ConstraintX ? 1 : 0) + (n.ConstraintY ? 1 : 0) + (n.ConstraintZ ? 1 : 0));
        
        if (totalConstraints < 6)
            throw new InvalidOperationException("Structure may be unstable. At least 6 constraints are required for 3D stability.");
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
}

/// <summary>
/// Contains the results of a structural analysis.
/// </summary>
public class AnalysisResult
{
    public List<Node> Nodes { get; init; } = new();
    public List<Element> Elements { get; init; } = new();
    public bool EquilibriumSatisfied { get; init; }
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
