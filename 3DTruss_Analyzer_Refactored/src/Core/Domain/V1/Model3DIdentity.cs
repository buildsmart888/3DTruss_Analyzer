namespace TrussAnalyzer.Core.Domain.V1;

/// <summary>Explicit copy operations create new identity; record updates, delete/undo, and import preserve it.</summary>
public static class Model3DIdentity
{
    public static Node3D Copy(Node3D source, string? label = null) => source with
    {
        Id = Guid.NewGuid(),
        Label = label ?? $"{source.Label} Copy",
        SpringIds = new List<Guid>(source.SpringIds),
        PrescribedMovementIds = new List<Guid>(source.PrescribedMovementIds)
    };

    public static T Preserve<T>(T source) where T : IPersistentModelObject => source;
}
