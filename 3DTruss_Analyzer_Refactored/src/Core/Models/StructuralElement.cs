namespace TrussAnalyzer.Core.Models;

public enum ElementType
{
    Truss,
    Frame3D
}

public abstract class StructuralElement
{
    public int Id { get; init; }
    public int StartNodeId { get; init; }
    public int EndNodeId { get; init; }
    public int MaterialId { get; init; }
    public int SectionId { get; init; }
    public ElementType Type { get; protected init; }
    public double RollAngleRadians { get; init; }
    public FrameMemberRelease Releases { get; init; } = new();

    public double Length(Point3D start, Point3D end) => start.DistanceTo(end);
}

public class FrameMemberRelease
{
    public bool StartMomentY { get; init; }
    public bool StartMomentZ { get; init; }
    public bool EndMomentY { get; init; }
    public bool EndMomentZ { get; init; }

    public bool HasAny => StartMomentY || StartMomentZ || EndMomentY || EndMomentZ;
}

public sealed class TrussElement : StructuralElement
{
    public TrussElement()
    {
        Type = ElementType.Truss;
    }

    public TrussElement(int id, int startNodeId, int endNodeId, int materialId, int sectionId)
    {
        Id = id;
        StartNodeId = startNodeId;
        EndNodeId = endNodeId;
        MaterialId = materialId;
        SectionId = sectionId;
        Type = ElementType.Truss;
    }
}

public sealed class FrameElement3D : StructuralElement
{
    public FrameElement3D()
    {
        Type = ElementType.Frame3D;
    }

    public FrameElement3D(int id, int startNodeId, int endNodeId, int materialId, int sectionId)
    {
        Id = id;
        StartNodeId = startNodeId;
        EndNodeId = endNodeId;
        MaterialId = materialId;
        SectionId = sectionId;
        Type = ElementType.Frame3D;
    }
}
