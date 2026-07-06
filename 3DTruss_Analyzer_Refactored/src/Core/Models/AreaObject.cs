namespace TrussAnalyzer.Core.Models;

public enum AreaObjectType
{
    Slab,
    Wall,
    Shell,
    Diaphragm
}

public enum AreaObjectAnalysisBehavior
{
    NotAnalyzed,
    RigidDiaphragmPlaceholder,
    ShellPlaceholder
}

/// <summary>
/// Prototype surface object for slab, wall, shell, and diaphragm modeling.
/// Area objects are model data only in Phase 8 and are not assembled into the frame solver.
/// </summary>
public class AreaObject
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public AreaObjectType Type { get; init; } = AreaObjectType.Slab;
    public List<int> NodeIds { get; init; } = new();
    public int MaterialId { get; init; }
    public double Thickness { get; init; }
    public string DiaphragmId { get; init; } = string.Empty;
    public AreaObjectAnalysisBehavior AnalysisBehavior { get; init; } = AreaObjectAnalysisBehavior.NotAnalyzed;
    public string Description { get; init; } = string.Empty;

    public bool IsTriangularOrQuadrilateral => NodeIds.Count is 3 or 4;

    public static AreaObject Slab(
        int id,
        string name,
        IEnumerable<int> nodeIds,
        int materialId,
        double thickness,
        string diaphragmId = "") => new()
        {
            Id = id,
            Name = name,
            Type = AreaObjectType.Slab,
            NodeIds = nodeIds.ToList(),
            MaterialId = materialId,
            Thickness = thickness,
            DiaphragmId = diaphragmId,
            AnalysisBehavior = string.IsNullOrWhiteSpace(diaphragmId)
                ? AreaObjectAnalysisBehavior.NotAnalyzed
                : AreaObjectAnalysisBehavior.RigidDiaphragmPlaceholder,
            Description = "Prototype slab area object; not included in frame analysis."
        };

    public static AreaObject Wall(
        int id,
        string name,
        IEnumerable<int> nodeIds,
        int materialId,
        double thickness) => new()
        {
            Id = id,
            Name = name,
            Type = AreaObjectType.Wall,
            NodeIds = nodeIds.ToList(),
            MaterialId = materialId,
            Thickness = thickness,
            AnalysisBehavior = AreaObjectAnalysisBehavior.NotAnalyzed,
            Description = "Prototype wall area object; not included in frame analysis."
        };

    public override string ToString() => $"{Type} area {Id}: {Name}";
}
