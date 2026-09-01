namespace TrussAnalyzer.Core.Analysis;

using TrussAnalyzer.Core.Models;

public abstract record MemberDiagramLoad(int ElementId);

public sealed record MemberPointDiagramLoad(
    int ElementId,
    double RelativePosition,
    Vector3D Force,
    Vector3D Moment) : MemberDiagramLoad(ElementId);

public sealed record MemberDistributedDiagramLoad(
    int ElementId,
    double StartRelativePosition,
    double EndRelativePosition,
    Vector3D ForcePerLength) : MemberDiagramLoad(ElementId);

public sealed class LoadAssemblyResult
{
    private readonly Dictionary<int, double[]> _equivalentElementLoadsLocal = new();
    private readonly Dictionary<int, List<MemberDiagramLoad>> _memberDiagramLoadsLocal = new();

    public LoadAssemblyResult(int totalDof)
    {
        if (totalDof < 0)
            throw new ArgumentOutOfRangeException(nameof(totalDof));

        GlobalLoadVector = new double[totalDof];
    }

    public double[] GlobalLoadVector { get; }

    public IReadOnlyDictionary<int, double[]> EquivalentElementLoadsLocal => _equivalentElementLoadsLocal;
    public IReadOnlyDictionary<int, IReadOnlyList<MemberDiagramLoad>> MemberDiagramLoadsLocal =>
        _memberDiagramLoadsLocal.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<MemberDiagramLoad>)entry.Value);

    internal double[] GetOrCreateEquivalentElementLoad(int elementId)
    {
        if (!_equivalentElementLoadsLocal.TryGetValue(elementId, out var equivalentLoad))
        {
            equivalentLoad = new double[12];
            _equivalentElementLoadsLocal[elementId] = equivalentLoad;
        }

        return equivalentLoad;
    }

    internal void AddMemberDiagramLoad(MemberDiagramLoad load)
    {
        if (!_memberDiagramLoadsLocal.TryGetValue(load.ElementId, out var loads))
        {
            loads = new List<MemberDiagramLoad>();
            _memberDiagramLoadsLocal[load.ElementId] = loads;
        }

        loads.Add(load);
    }
}
