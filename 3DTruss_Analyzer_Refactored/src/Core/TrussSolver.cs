using System;
using System.Collections.Generic;
using _3DTrussAnalyzer.Models;
using _3DTrussAnalyzer.Utilities;

namespace _3DTrussAnalyzer.Core
{
    /// <summary>
    /// Result of a truss analysis operation
    /// </summary>
    public class AnalysisResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public int NumberOfNodes { get; set; }
        public int NumberOfElements { get; set; }
        public int TotalDOF { get; set; }
        public int FreeDOF { get; set; }
        public double[] Displacements { get; set; }
        public double[] Reactions { get; set; }
        public Dictionary<int, double> ElementForces { get; set; }
        public double TotalAppliedForceX { get; set; }
        public double TotalAppliedForceY { get; set; }
        public double TotalAppliedForceZ { get; set; }
        public double TotalReactionForceX { get; set; }
        public double TotalReactionForceY { get; set; }
        public double TotalReactionForceZ { get; set; }
        public bool EquilibriumSatisfied { get; set; }
        public double EquilibriumErrorX { get; set; }
        public double EquilibriumErrorY { get; set; }
        public double EquilibriumErrorZ { get; set; }
    }

    /// <summary>
    /// 3D Truss Solver using Finite Element Method
    /// Implements direct stiffness method for space truss analysis
    /// </summary>
    public class TrussSolver
    {
        private readonly List<Node> _nodes;
        private readonly List<Element> _elements;
        private readonly double _gravityAcceleration;

        public TrussSolver(List<Node> nodes, List<Element> elements, double gravityAcceleration = 9.81)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _elements = elements ?? throw new ArgumentNullException(nameof(elements));
            _gravityAcceleration = gravityAcceleration;
        }

        /// <summary>
        /// Performs complete truss analysis
        /// </summary>
        public AnalysisResult Analyze(bool includeSelfWeight = false, 
                                     double accelX = 0, double accelY = 0, double accelZ = -1)
        {
            var result = new AnalysisResult();
            
            try
            {
                var validationErrors = ValidateModel();
                if (validationErrors.Count > 0)
                {
                    result.Success = false;
                    result.ErrorMessage = string.Join("\n", validationErrors);
                    return result;
                }

                result.NumberOfNodes = _nodes.Count;
                result.NumberOfElements = _elements.Count;

                foreach (var element in _elements)
                {
                    var startNode = GetNode(element.StartNodeId);
                    var endNode = GetNode(element.EndNodeId);
                    element.UpdateGeometry(startNode.Coordinates, endNode.Coordinates);
                }

                int totalDOF = _nodes.Count * 3;
                result.TotalDOF = totalDOF;

                var globalStiffness = new Matrix(totalDOF, totalDOF);
                var globalForce = new Matrix(totalDOF, 1);
                var dofConstraint = new int[totalDOF];

                for (int i = 0; i < _nodes.Count; i++)
                {
                    var node = _nodes[i];
                    dofConstraint[i * 3 + 0] = node.Constraint.IsXFixed ? -1 : 0;
                    dofConstraint[i * 3 + 1] = node.Constraint.IsYFixed ? -1 : 0;
                    dofConstraint[i * 3 + 2] = node.Constraint.IsZFixed ? -1 : 0;
                }

                for (int i = 0; i < _nodes.Count; i++)
                {
                    var node = _nodes[i];
                    globalForce[i * 3 + 0, 0] = node.AppliedForce.X;
                    globalForce[i * 3 + 1, 0] = node.AppliedForce.Y;
                    globalForce[i * 3 + 2, 0] = node.AppliedForce.Z;

                    result.TotalAppliedForceX += node.AppliedForce.X;
                    result.TotalAppliedForceY += node.AppliedForce.Y;
                    result.TotalAppliedForceZ += node.AppliedForce.Z;
                }

                if (includeSelfWeight)
                {
                    AddSelfWeight(globalForce, accelX, accelY, accelZ);
                }

                AssembleStiffnessMatrix(globalStiffness);

                int freeDOF = 0;
                for (int i = 0; i < totalDOF; i++)
                {
                    if (dofConstraint[i] == 0) freeDOF++;
                }
                result.FreeDOF = freeDOF;

                if (freeDOF == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "No free degrees of freedom.";
                    return result;
                }

                var solution = ApplyBoundaryConditionsAndSolve(
                    globalStiffness, globalForce, dofConstraint, totalDOF, freeDOF);

                if (!solution.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = solution.ErrorMessage;
                    return result;
                }

                ExtractResults(solution.Displacements, result, totalDOF);
                CalculateReactions(globalStiffness, solution.Displacements, globalForce, result);
                CalculateElementForces(result);
                CheckEquilibrium(result);

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Analysis failed: {ex.Message}";
            }

            return result;
        }

        private List<string> ValidateModel()
        {
            var errors = new List<string>();
            if (_nodes.Count == 0) errors.Add("No nodes defined");
            if (_elements.Count == 0) errors.Add("No elements defined");

            bool hasConstraints = false;
            foreach (var node in _nodes)
            {
                if (node.IsConstrained) { hasConstraints = true; break; }
            }
            if (!hasConstraints) errors.Add("No constraints defined. Structure is unstable.");

            return errors;
        }

        private Node GetNode(int nodeId) => _nodes.Find(n => n.Id == nodeId);

        private void AddSelfWeight(Matrix forceVector, double ax, double ay, double az)
        {
            foreach (var element in _elements)
            {
                double totalWeight = element.Material.Density * element.Area * element.Length * _gravityAcceleration;
                double fx = (ax / Math.Abs(az)) * (totalWeight / 2.0);
                double fy = (ay / Math.Abs(az)) * (totalWeight / 2.0);
                double fz = (az / Math.Abs(az)) * (totalWeight / 2.0);

                var startNode = GetNode(element.StartNodeId);
                var endNode = GetNode(element.EndNodeId);
                int startIdx = _nodes.IndexOf(startNode) * 3;
                int endIdx = _nodes.IndexOf(endNode) * 3;

                forceVector[startIdx + 0, 0] += fx;
                forceVector[startIdx + 1, 0] += fy;
                forceVector[startIdx + 2, 0] += fz;
                forceVector[endIdx + 0, 0] += fx;
                forceVector[endIdx + 1, 0] += fy;
                forceVector[endIdx + 2, 0] += fz;
            }
        }

        private void AssembleStiffnessMatrix(Matrix globalStiffness)
        {
            foreach (var element in _elements)
            {
                var startNode = GetNode(element.StartNodeId);
                var endNode = GetNode(element.EndNodeId);
                int startIdx = _nodes.IndexOf(startNode) * 3;
                int endIdx = _nodes.IndexOf(endNode) * 3;
                var elemStiffness = CalculateElementStiffnessMatrix(element);
                int[] dofIndices = { startIdx, startIdx + 1, startIdx + 2, endIdx, endIdx + 1, endIdx + 2 };

                for (int i = 0; i < 6; i++)
                    for (int j = 0; j < 6; j++)
                        globalStiffness[dofIndices[i], dofIndices[j]] += elemStiffness[i, j];
            }
        }

        private Matrix CalculateElementStiffnessMatrix(Element element)
        {
            double k = element.StiffnessCoefficient;
            var lc = element.DirectionCosines;
            var stiffness = new Matrix(6, 6);

            double[,] cMatrix = {
                { lc.X * lc.X, lc.X * lc.Y, lc.X * lc.Z },
                { lc.Y * lc.X, lc.Y * lc.Y, lc.Y * lc.Z },
                { lc.Z * lc.X, lc.Z * lc.Y, lc.Z * lc.Z }
            };

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    stiffness[i, j] = k * cMatrix[i, j];
                    stiffness[i + 3, j + 3] = k * cMatrix[i, j];
                    stiffness[i, j + 3] = -k * cMatrix[i, j];
                    stiffness[i + 3, j] = -k * cMatrix[i, j];
                }
            }
            return stiffness;
        }

        private (bool Success, string ErrorMessage, double[] Displacements) ApplyBoundaryConditionsAndSolve(
            Matrix globalStiffness, Matrix globalForce, int[] dofConstraint, int totalDOF, int freeDOF)
        {
            var reducedStiffness = new Matrix(freeDOF, freeDOF);
            var reducedForce = new Matrix(freeDOF, 1);
            int[] freeDOFIndices = new int[freeDOF];
            int freeCount = 0;

            for (int i = 0; i < totalDOF; i++)
            {
                if (dofConstraint[i] == 0) { freeDOFIndices[freeCount] = i; freeCount++; }
            }

            for (int i = 0; i < freeDOF; i++)
            {
                reducedForce[i, 0] = globalForce[freeDOFIndices[i], 0];
                for (int j = 0; j < freeDOF; j++)
                    reducedStiffness[i, j] = globalStiffness[freeDOFIndices[i], freeDOFIndices[j]];
            }

            var reducedDisplacement = new Matrix(freeDOF, 1);
            bool solved = MatrixSolver.GaussianElimination(reducedStiffness, reducedForce, reducedDisplacement);

            if (!solved)
                return (false, "Failed to solve system. Structure may be unstable.", null);

            double[] fullDisplacements = new double[totalDOF];
            for (int i = 0; i < freeDOF; i++)
                fullDisplacements[freeDOFIndices[i]] = reducedDisplacement[i, 0];

            return (true, null, fullDisplacements);
        }

        private void ExtractResults(double[] displacements, AnalysisResult result, int totalDOF)
        {
            result.Displacements = displacements;
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].Displacement = new Vector3D(
                    displacements[i * 3 + 0], displacements[i * 3 + 1], displacements[i * 3 + 2]);
            }
        }

        private void CalculateReactions(Matrix globalStiffness, double[] displacements, 
                                       Matrix appliedForce, AnalysisResult result)
        {
            var reactions = new Matrix(globalStiffness.Rows, 1);
            for (int i = 0; i < globalStiffness.Rows; i++)
            {
                double ku = 0;
                for (int j = 0; j < globalStiffness.Cols; j++)
                    ku += globalStiffness[i, j] * displacements[j];
                reactions[i, 0] = ku - appliedForce[i, 0];
            }

            result.Reactions = new double[reactions.Rows];
            for (int i = 0; i < reactions.Rows; i++)
            {
                result.Reactions[i] = reactions[i, 0];
                _nodes[i / 3].ReactionForce = i % 3 == 0 ? 
                    new Vector3D(reactions[i, 0], reactions[i + 1, 0], reactions[i + 2, 0]) :
                    _nodes[i / 3].ReactionForce;
            }

            for (int i = 0; i < _nodes.Count; i++)
            {
                result.TotalReactionForceX += _nodes[i].ReactionForce.X;
                result.TotalReactionForceY += _nodes[i].ReactionForce.Y;
                result.TotalReactionForceZ += _nodes[i].ReactionForce.Z;
            }
        }

        private void CalculateElementForces(AnalysisResult result)
        {
            result.ElementForces = new Dictionary<int, double>();
            foreach (var element in _elements)
            {
                var startNode = GetNode(element.StartNodeId);
                var endNode = GetNode(element.EndNodeId);
                double dx = endNode.Displacement.X - startNode.Displacement.X;
                double dy = endNode.Displacement.Y - startNode.Displacement.Y;
                double dz = endNode.Displacement.Z - startNode.Displacement.Z;

                double axialDeformation = dx * element.DirectionCosines.X +
                                         dy * element.DirectionCosines.Y +
                                         dz * element.DirectionCosines.Z;

                element.AxialDeformation = axialDeformation;
                element.AxialForce = element.StiffnessCoefficient * axialDeformation;
                element.AxialStress = element.AxialForce / element.Area;
                result.ElementForces[element.Id] = element.AxialForce;
            }
        }

        private void CheckEquilibrium(AnalysisResult result)
        {
            result.EquilibriumErrorX = result.TotalAppliedForceX + result.TotalReactionForceX;
            result.EquilibriumErrorY = result.TotalAppliedForceY + result.TotalReactionForceY;
            result.EquilibriumErrorZ = result.TotalAppliedForceZ + result.TotalReactionForceZ;

            double tolerance = 1e-6;
            double maxForce = Math.Max(Math.Abs(result.TotalAppliedForceX), 
                              Math.Max(Math.Abs(result.TotalAppliedForceY), 
                                      Math.Abs(result.TotalAppliedForceZ)));
            if (maxForce < 1e-10) maxForce = 1.0;

            result.EquilibriumSatisfied = 
                Math.Abs(result.EquilibriumErrorX) / maxForce < tolerance &&
                Math.Abs(result.EquilibriumErrorY) / maxForce < tolerance &&
                Math.Abs(result.EquilibriumErrorZ) / maxForce < tolerance;

            if (!result.EquilibriumSatisfied)
            {
                result.Warnings.Add("WARNING: Equilibrium check failed.");
                result.Warnings.Add($"  Force error X: {result.EquilibriumErrorX:E3} N");
                result.Warnings.Add($"  Force error Y: {result.EquilibriumErrorY:E3} N");
                result.Warnings.Add($"  Force error Z: {result.EquilibriumErrorZ:E3} N");
            }
        }
    }
}
