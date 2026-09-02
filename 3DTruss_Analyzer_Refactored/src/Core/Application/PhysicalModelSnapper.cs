namespace TrussAnalyzer.Core.Application;

using TrussAnalyzer.Core.Domain.V1;

/// <summary>Deterministic coordinate snapping for physical-authoring tools. Endpoint snapping wins over grid snapping.</summary>
public sealed class PhysicalModelSnapper
{
    public PhysicalSnapResult Snap(ProjectDocument document, Point3DValue requested, double gridSpacing = 1.0, double endpointTolerance = 0.15)
    {
        if (gridSpacing <= 0 || double.IsNaN(gridSpacing) || double.IsInfinity(gridSpacing))
            throw new ArgumentOutOfRangeException(nameof(gridSpacing));
        if (endpointTolerance < 0 || double.IsNaN(endpointTolerance) || double.IsInfinity(endpointTolerance))
            throw new ArgumentOutOfRangeException(nameof(endpointTolerance));

        var endpoint = document.Model.Nodes
            .Select(node => new { node, DistanceSquared = DistanceSquared(node.Position, requested) })
            .Where(candidate => candidate.DistanceSquared <= endpointTolerance * endpointTolerance)
            .OrderBy(candidate => candidate.DistanceSquared)
            .ThenBy(candidate => candidate.node.Id)
            .FirstOrDefault();
        if (endpoint is not null)
            return new(endpoint.node.Position, PhysicalSnapKind.Endpoint, endpoint.node.Id);

        return new(new(
            Math.Round(requested.X / gridSpacing, MidpointRounding.AwayFromZero) * gridSpacing,
            Math.Round(requested.Y / gridSpacing, MidpointRounding.AwayFromZero) * gridSpacing,
            Math.Round(requested.Z / gridSpacing, MidpointRounding.AwayFromZero) * gridSpacing), PhysicalSnapKind.Grid, null);
    }

    private static double DistanceSquared(Point3DValue a, Point3DValue b)
    {
        var dx = a.X - b.X; var dy = a.Y - b.Y; var dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }
}

public enum PhysicalSnapKind { Grid, Endpoint }
public sealed record PhysicalSnapResult(Point3DValue Position, PhysicalSnapKind Kind, Guid? EndpointNodeId);
