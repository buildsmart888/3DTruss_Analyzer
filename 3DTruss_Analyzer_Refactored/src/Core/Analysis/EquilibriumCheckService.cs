namespace TrussAnalyzer.Core.Analysis;

using TrussAnalyzer.Core.Models;

public sealed class EquilibriumCheckService
{
    public EquilibriumCheck Calculate(
        IReadOnlyList<Node> nodes,
        IReadOnlyList<StructuralNodeResult> nodeResults,
        double[] originalLoadVector,
        DofIndexer dofIndexer)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(nodeResults);
        ArgumentNullException.ThrowIfNull(originalLoadVector);
        ArgumentNullException.ThrowIfNull(dofIndexer);

        if (nodeResults.Count != nodes.Count)
            throw new ArgumentException("A node result is required for each model node.", nameof(nodeResults));
        if (originalLoadVector.Length != dofIndexer.TotalDof)
            throw new ArgumentException("Original load vector length must match the total DOF count.", nameof(originalLoadVector));

        var resultsByNodeId = nodeResults.ToDictionary(result => result.NodeId);
        double sumFx = 0;
        double sumFy = 0;
        double sumFz = 0;
        double scale = 0;

        foreach (var node in nodes)
        {
            if (!resultsByNodeId.TryGetValue(node.Id, out var result))
                throw new ArgumentException($"Node result for node {node.Id} is missing.", nameof(nodeResults));

            int dofBase = dofIndexer.GetNodeDofBase(node.Id);
            sumFx += originalLoadVector[dofBase] + result.ReactionForce.X;
            sumFy += originalLoadVector[dofBase + 1] + result.ReactionForce.Y;
            sumFz += originalLoadVector[dofBase + 2] + result.ReactionForce.Z;
            scale += Math.Abs(originalLoadVector[dofBase]) +
                Math.Abs(originalLoadVector[dofBase + 1]) +
                Math.Abs(originalLoadVector[dofBase + 2]) +
                result.ReactionForce.Magnitude;
        }

        return new EquilibriumCheck(sumFx, sumFy, sumFz, Math.Max(1e-6, scale * 1e-9));
    }
}
