namespace TrussAnalyzer.Core.Models;

public enum NodeDof
{
    UX,
    UY,
    UZ,
    RX,
    RY,
    RZ
}

public enum LoadDirection
{
    GlobalX,
    GlobalY,
    GlobalZ,
    LocalX,
    LocalY,
    LocalZ
}

public abstract class LoadItem
{
    public string LoadCaseId { get; init; } = string.Empty;
}

public sealed class NodalLoad : LoadItem
{
    public int NodeId { get; init; }
    public Vector3D Force { get; init; } = Vector3D.Zero;
    public Vector3D Moment { get; init; } = Vector3D.Zero;
}

public sealed class MemberPointLoad : LoadItem
{
    public int ElementId { get; init; }
    public double RelativeDistance { get; init; } = 0.5;
    public Vector3D Force { get; init; } = Vector3D.Zero;
    public Vector3D Moment { get; init; } = Vector3D.Zero;
    public LoadDirection Direction { get; init; } = LoadDirection.GlobalZ;
}

public sealed class MemberDistributedLoad : LoadItem
{
    public int ElementId { get; init; }
    public Vector3D ForcePerLength { get; init; } = Vector3D.Zero;
    public LoadDirection Direction { get; init; } = LoadDirection.GlobalZ;
}
