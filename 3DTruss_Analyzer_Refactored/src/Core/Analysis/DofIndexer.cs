namespace TrussAnalyzer.Core.Analysis;

using TrussAnalyzer.Core.Models;

public sealed class DofIndexer
{
    public const int DofPerNode = 6;
    private readonly IReadOnlyList<Node> _nodes;
    private readonly Dictionary<int, int> _nodeIndex = new();

    public DofIndexer(IReadOnlyList<Node> nodes)
    {
        _nodes = nodes;
        for (int i = 0; i < nodes.Count; i++)
            _nodeIndex[nodes[i].Id] = i;
    }

    public int NodeCount => _nodes.Count;
    public int TotalDof => _nodes.Count * DofPerNode;

    public int ConstrainedDof => _nodes.Sum(n =>
        (n.ConstraintX ? 1 : 0) +
        (n.ConstraintY ? 1 : 0) +
        (n.ConstraintZ ? 1 : 0) +
        (n.ConstraintRX ? 1 : 0) +
        (n.ConstraintRY ? 1 : 0) +
        (n.ConstraintRZ ? 1 : 0));

    public int GetNodeDofBase(int nodeId)
    {
        if (!_nodeIndex.TryGetValue(nodeId, out int index))
            throw new KeyNotFoundException($"Node {nodeId} was not found in the DOF index.");

        return index * DofPerNode;
    }

    public int[] GetElementDofMap(StructuralElement element)
    {
        int start = GetNodeDofBase(element.StartNodeId);
        int end = GetNodeDofBase(element.EndNodeId);
        return new[]
        {
            start, start + 1, start + 2, start + 3, start + 4, start + 5,
            end, end + 1, end + 2, end + 3, end + 4, end + 5
        };
    }

    public IEnumerable<int> GetConstrainedDofs(Node node)
    {
        int start = GetNodeDofBase(node.Id);
        if (node.ConstraintX) yield return start;
        if (node.ConstraintY) yield return start + 1;
        if (node.ConstraintZ) yield return start + 2;
        if (node.ConstraintRX) yield return start + 3;
        if (node.ConstraintRY) yield return start + 4;
        if (node.ConstraintRZ) yield return start + 5;
    }

    public IEnumerable<KeyValuePair<int, double>> GetConstrainedDofValues(Node node)
    {
        int start = GetNodeDofBase(node.Id);
        if (node.ConstraintX) yield return new KeyValuePair<int, double>(start, node.PrescribedDisplacement.X);
        if (node.ConstraintY) yield return new KeyValuePair<int, double>(start + 1, node.PrescribedDisplacement.Y);
        if (node.ConstraintZ) yield return new KeyValuePair<int, double>(start + 2, node.PrescribedDisplacement.Z);
        if (node.ConstraintRX) yield return new KeyValuePair<int, double>(start + 3, node.PrescribedRotation.X);
        if (node.ConstraintRY) yield return new KeyValuePair<int, double>(start + 4, node.PrescribedRotation.Y);
        if (node.ConstraintRZ) yield return new KeyValuePair<int, double>(start + 5, node.PrescribedRotation.Z);
    }
}
