namespace TrussAnalyzer.Core.Domain.V1;

using System.Text.Json.Serialization;

public interface IPersistentModelObject
{
    Guid Id { get; }
    string Label { get; }
}

public readonly record struct Point3DValue(double X, double Y, double Z)
{
    public Vector3DValue VectorTo(Point3DValue other) => new(other.X - X, other.Y - Y, other.Z - Z);
}

public readonly record struct Vector3DValue(double X, double Y, double Z)
{
    [JsonIgnore]
    public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z);
    public double Dot(Vector3DValue other) => X * other.X + Y * other.Y + Z * other.Z;
    public Vector3DValue Cross(Vector3DValue other) => new(
        Y * other.Z - Z * other.Y,
        Z * other.X - X * other.Z,
        X * other.Y - Y * other.X);
    public Vector3DValue Scale(double factor) => new(X * factor, Y * factor, Z * factor);
    public Vector3DValue Normalize() => Magnitude > 1e-12
        ? Scale(1.0 / Magnitude)
        : throw new InvalidOperationException("A zero vector cannot be normalized.");
}

/// <summary>Six global or local components ordered UX, UY, UZ, RX, RY, RZ.</summary>
public sealed record DofValues(
    double UX = 0, double UY = 0, double UZ = 0,
    double RX = 0, double RY = 0, double RZ = 0);

public sealed record DofRestraints(
    bool UX = false, bool UY = false, bool UZ = false,
    bool RX = false, bool RY = false, bool RZ = false)
{
    [JsonIgnore]
    public bool All => UX && UY && UZ && RX && RY && RZ;
    [JsonIgnore]
    public bool Any => UX || UY || UZ || RX || RY || RZ;
}

public sealed record SourceMetadata
{
    public string SourceSystem { get; init; } = string.Empty;
    public string SourceVersion { get; init; } = string.Empty;
    public string SourceObjectId { get; init; } = string.Empty;
    public string Revision { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public sealed record ProjectInfo
{
    public string Name { get; init; } = "Untitled Project";
    public string ProjectNumber { get; init; } = string.Empty;
    public string Engineer { get; init; } = string.Empty;
    public string Reviewer { get; init; } = string.Empty;
}

public enum LengthDisplayUnit { Meter, Centimeter, Millimeter }
public enum ForceDisplayUnit { Newton, Kilonewton, KilogramForce, TonneForce }
public enum StressDisplayUnit { Pascal, Megapascal }

/// <summary>Display preferences only. Serialized engineering values always remain canonical SI.</summary>
public sealed record UnitPreferences
{
    public LengthDisplayUnit Length { get; init; } = LengthDisplayUnit.Meter;
    public ForceDisplayUnit Force { get; init; } = ForceDisplayUnit.Newton;
    public StressDisplayUnit Stress { get; init; } = StressDisplayUnit.Pascal;
}

public sealed record ProjectDocument
{
    public const string CurrentSchemaVersion = "1.0";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public ProjectInfo ProjectInfo { get; init; } = new();
    public UnitPreferences UnitPreferences { get; init; } = new();
    public Model3D Model { get; init; } = new();
    public LoadDefinitions LoadDefinitions { get; init; } = new();
    public AnalysisDefinitions AnalysisDefinitions { get; init; } = new();
    public PresentationSettings PresentationSettings { get; init; } = new();
    public AuditMetadata AuditMetadata { get; init; } = new();
}

public sealed record AuditMetadata
{
    public Guid DocumentId { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedUtc { get; init; } = DateTimeOffset.UtcNow;
    public string CreatedByVersion { get; init; } = string.Empty;
}

public sealed record PresentationSettings
{
    public string Theme { get; init; } = "System";
    public string ActiveView { get; init; } = "Isometric";
}

public sealed record Model3D
{
    public List<Node3D> Nodes { get; init; } = new();
    public List<LineObject3D> LineObjects { get; init; } = new();
    public List<AreaObject3D> AreaObjects { get; init; } = new();
    public List<Material3D> Materials { get; init; } = new();
    public List<Section3D> Sections { get; init; } = new();
    public List<SupportDefinition> Supports { get; init; } = new();
    public List<SpringDefinition> Springs { get; init; } = new();
    public List<PrescribedMovementDefinition> PrescribedMovements { get; init; } = new();
    public List<RigidLink3D> RigidLinks { get; init; } = new();
    public List<MasterSlaveConstraint3D> Constraints { get; init; } = new();
    public List<Level3D> Levels { get; init; } = new();
    public List<GridLine3D> Grids { get; init; } = new();
    public List<ModelGroup3D> Groups { get; init; } = new();
}

public sealed record Node3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public Point3DValue Position { get; init; }
    public Guid? SupportId { get; init; }
    public List<Guid> SpringIds { get; init; } = new();
    public List<Guid> PrescribedMovementIds { get; init; } = new();
    public SourceMetadata Source { get; init; } = new();
}

public sealed record SupportDefinition : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public DofRestraints Restrained { get; init; } = new();
}

public sealed record SpringDefinition : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    /// <summary>UX-UZ are N/m and RX-RZ are N-m/rad.</summary>
    public DofValues Stiffness { get; init; } = new();
}

public sealed record PrescribedMovementDefinition : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public Guid LoadPatternId { get; init; }
    /// <summary>UX-UZ are metres and RX-RZ are radians.</summary>
    public DofValues Movement { get; init; } = new();
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "objectType")]
[JsonDerivedType(typeof(Frame3D), "frame3D")]
[JsonDerivedType(typeof(Truss3D), "truss3D")]
public abstract record LineObject3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public Guid StartNodeId { get; init; }
    public Guid EndNodeId { get; init; }
    public Guid MaterialId { get; init; }
    public Guid SectionId { get; init; }
    public LocalAxisReference LocalAxis { get; init; } = new();
    public Vector3DValue StartInsertionOffsetLocal { get; init; }
    public Vector3DValue EndInsertionOffsetLocal { get; init; }
    public double StartRigidOffset { get; init; }
    public double EndRigidOffset { get; init; }
    public EndRelease6 StartRelease { get; init; } = new();
    public EndRelease6 EndRelease { get; init; } = new();
    public SourceMetadata Source { get; init; } = new();
}

public sealed record Frame3D : LineObject3D;
public sealed record Truss3D : LineObject3D;

public sealed record LocalAxisReference
{
    /// <summary>Global reference vector projected normal to member local x before roll is applied.</summary>
    public Vector3DValue ReferenceVector { get; init; } = new(0, 0, 1);
    public double RollRadians { get; init; }
}

public sealed record EndRelease6
{
    public DofRestraints Released { get; init; } = new();
    /// <summary>Optional finite connection stiffness for each released DOF.</summary>
    public DofValues? PartialFixity { get; init; }
}

public enum MaterialKind { Steel, Concrete, Aluminum, Timber, Custom }

public sealed record Material3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public MaterialKind Kind { get; init; } = MaterialKind.Custom;
    public double YoungsModulus { get; init; }
    public double ShearModulus { get; init; }
    public double PoissonsRatio { get; init; }
    public double Density { get; init; }
    public double ThermalExpansionCoefficient { get; init; }
    public SourceMetadata Source { get; init; } = new();
}

public enum SectionShape { Generic, Rectangle, Circle, Pipe, IShape, Channel, Angle, Box, Custom }

public sealed record Section3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public SectionShape Shape { get; init; } = SectionShape.Generic;
    public double Area { get; init; }
    public double Iy { get; init; }
    public double Iz { get; init; }
    public double TorsionalConstant { get; init; }
    public double ShearAreaY { get; init; }
    public double ShearAreaZ { get; init; }
    /// <summary>Display-only dimensions in metres; they never derive analysis properties implicitly.</summary>
    public Dictionary<string, double> DisplayDimensions { get; init; } = new(StringComparer.Ordinal);
    public SourceMetadata Source { get; init; } = new();
}

public sealed record Level3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public double Elevation { get; init; }
}

public enum GridAxis3D { X, Y }

public sealed record GridLine3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public GridAxis3D Axis { get; init; }
    public double Coordinate { get; init; }
}

public sealed record ModelGroup3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public List<Guid> ObjectIds { get; init; } = new();
}

public enum AreaAnalysisBehavior { StorageOnly }

public sealed record AreaObject3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public List<Guid> BoundaryNodeIds { get; init; } = new();
    public Guid? MaterialId { get; init; }
    public Guid? SectionId { get; init; }
    public AreaAnalysisBehavior AnalysisBehavior { get; init; } = AreaAnalysisBehavior.StorageOnly;
    public SourceMetadata Source { get; init; } = new();
}

public sealed record RigidLink3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public Guid MasterNodeId { get; init; }
    public Guid SlaveNodeId { get; init; }
    public DofRestraints CoupledDofs { get; init; } = new(true, true, true, true, true, true);
}

public sealed record MasterSlaveConstraint3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public Guid MasterNodeId { get; init; }
    public List<Guid> SlaveNodeIds { get; init; } = new();
    public DofRestraints CoupledDofs { get; init; } = new(true, true, true);
}

public enum LoadPatternKind { Dead, SuperimposedDead, Live, RoofLive, Wind, Seismic, Temperature, Settlement, Other }

public sealed record LoadDefinitions
{
    public List<LoadPattern3D> LoadPatterns { get; init; } = new();
    public List<LoadAssignment3D> Assignments { get; init; } = new();
    public MassSource3D MassSource { get; init; } = new();
    public List<LoadCombination3D> LoadCombinations { get; init; } = new();
}

public sealed record LoadPattern3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public LoadPatternKind Kind { get; init; }
    public double SelfWeightMultiplier { get; init; }
    public SourceMetadata Source { get; init; } = new();
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "assignmentType")]
[JsonDerivedType(typeof(NodalLoadAssignment3D), "nodal")]
[JsonDerivedType(typeof(LineLoadAssignment3D), "line")]
public abstract record LoadAssignment3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public Guid LoadPatternId { get; init; }
    public SourceMetadata Source { get; init; } = new();
}

public sealed record NodalLoadAssignment3D : LoadAssignment3D
{
    public Guid NodeId { get; init; }
    public Vector3DValue Force { get; init; }
    public Vector3DValue Moment { get; init; }
}

public enum LoadCoordinateBasis { Global, Local }

public sealed record LineLoadAssignment3D : LoadAssignment3D
{
    public Guid LineObjectId { get; init; }
    public LoadCoordinateBasis Basis { get; init; }
    public Vector3DValue ForcePerLength { get; init; }
    public double StartRelativePosition { get; init; }
    public double EndRelativePosition { get; init; } = 1;
}

public sealed record MassSource3D
{
    public bool IncludeElementMass { get; init; } = true;
    public Dictionary<Guid, double> LoadPatternFactors { get; init; } = new();
}

public sealed record LoadCombination3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public Dictionary<Guid, double> LoadPatternFactors { get; init; } = new();
}

public sealed record AnalysisDefinitions
{
    public List<AnalysisCase3D> Cases { get; init; } = new();
    public SolverOptions3D SolverOptions { get; init; } = new();
    public List<ResultSelection3D> ResultSelections { get; init; } = new();
}

public sealed record AnalysisCase3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public Guid LoadPatternId { get; init; }
}

public sealed record SolverOptions3D
{
    public string Solver { get; init; } = "NativeDenseLinear";
    public double NumericalTolerance { get; init; } = 1e-10;
}

public enum ResultSelectionKind { LoadPattern, LoadCombination, Envelope }

public sealed record ResultSelection3D : IPersistentModelObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public ResultSelectionKind Kind { get; init; }
    public List<Guid> SourceIds { get; init; } = new();
}
