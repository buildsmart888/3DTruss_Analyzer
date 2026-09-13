namespace TrussAnalyzer.Core.Application;

using TrussAnalyzer.Core.Domain.V1;

/// <summary>Deterministic coordinate snapping for physical-authoring tools.</summary>
public sealed class PhysicalModelSnapper
{
    public PhysicalSnapResult Snap(ProjectDocument document, Point3DValue requested, double gridSpacing = 1.0, double endpointTolerance = 0.15, double geometryTolerance = 0.15)
    {
        if (gridSpacing <= 0 || double.IsNaN(gridSpacing) || double.IsInfinity(gridSpacing))
            throw new ArgumentOutOfRangeException(nameof(gridSpacing));
        if (endpointTolerance < 0 || double.IsNaN(endpointTolerance) || double.IsInfinity(endpointTolerance))
            throw new ArgumentOutOfRangeException(nameof(endpointTolerance));
        if (geometryTolerance < 0 || double.IsNaN(geometryTolerance) || double.IsInfinity(geometryTolerance))
            throw new ArgumentOutOfRangeException(nameof(geometryTolerance));

        var endpoint = document.Model.Nodes
            .Select(node => new { node, DistanceSquared = DistanceSquared(node.Position, requested) })
            .Where(candidate => candidate.DistanceSquared <= endpointTolerance * endpointTolerance)
            .OrderBy(candidate => candidate.DistanceSquared)
            .ThenBy(candidate => candidate.node.Id)
            .FirstOrDefault();
        if (endpoint is not null)
            return new(endpoint.node.Position, PhysicalSnapKind.Endpoint, endpoint.node.Id);

        var nodes = document.Model.Nodes.ToDictionary(node => node.Id);
        var intersection = FindIntersection(document, requested, geometryTolerance);
        if (intersection is not null)
            return new(intersection.Value.Position, PhysicalSnapKind.Intersection, null) { GeometryObjectId = intersection.Value.LineId };

        var midpoint = document.Model.LineObjects
            .Where(line => nodes.ContainsKey(line.StartNodeId) && nodes.ContainsKey(line.EndNodeId))
            .Select(line => new { line, Position = Midpoint(nodes[line.StartNodeId].Position, nodes[line.EndNodeId].Position) })
            .Select(candidate => new { candidate.line, candidate.Position, DistanceSquared = DistanceSquared(candidate.Position, requested) })
            .Where(candidate => candidate.DistanceSquared <= geometryTolerance * geometryTolerance)
            .OrderBy(candidate => candidate.DistanceSquared)
            .ThenBy(candidate => candidate.line.Id)
            .FirstOrDefault();
        if (midpoint is not null)
            return new(midpoint.Position, PhysicalSnapKind.Midpoint, null) { GeometryObjectId = midpoint.line.Id };

        return new(new(
            Math.Round(requested.X / gridSpacing, MidpointRounding.AwayFromZero) * gridSpacing,
            Math.Round(requested.Y / gridSpacing, MidpointRounding.AwayFromZero) * gridSpacing,
            Math.Round(requested.Z / gridSpacing, MidpointRounding.AwayFromZero) * gridSpacing), PhysicalSnapKind.Grid, null);
    }

    private static (Point3DValue Position, Guid LineId)? FindIntersection(ProjectDocument document, Point3DValue requested, double tolerance)
    {
        var nodes = document.Model.Nodes.ToDictionary(node => node.Id);
        var lines = document.Model.LineObjects
            .Where(line => nodes.ContainsKey(line.StartNodeId) && nodes.ContainsKey(line.EndNodeId))
            .Select(line => (line, Start: nodes[line.StartNodeId].Position, End: nodes[line.EndNodeId].Position))
            .ToArray();
        (Point3DValue Position, Guid LineId)? best = null;
        var bestDistance = double.PositiveInfinity;
        for (var i = 0; i < lines.Length; i++)
        for (var j = i + 1; j < lines.Length; j++)
        {
            var a = lines[i]; var b = lines[j];
            var denominator = (a.End.X - a.Start.X) * (b.End.Y - b.Start.Y) - (a.End.Y - a.Start.Y) * (b.End.X - b.Start.X);
            if (Math.Abs(denominator) < 1e-12) continue;
            var dx = b.Start.X - a.Start.X; var dy = b.Start.Y - a.Start.Y;
            var ta = (dx * (b.End.Y - b.Start.Y) - dy * (b.End.X - b.Start.X)) / denominator;
            var tb = (dx * (a.End.Y - a.Start.Y) - dy * (a.End.X - a.Start.X)) / denominator;
            if (ta < -1e-9 || ta > 1 + 1e-9 || tb < -1e-9 || tb > 1 + 1e-9) continue;
            var point = new Point3DValue(
                a.Start.X + ta * (a.End.X - a.Start.X),
                a.Start.Y + ta * (a.End.Y - a.Start.Y),
                a.Start.Z + ta * (a.End.Z - a.Start.Z));
            var distance = DistanceSquared(point, requested);
            var lineId = a.line.Id.CompareTo(b.line.Id) <= 0 ? a.line.Id : b.line.Id;
            if (distance <= tolerance * tolerance && (distance < bestDistance || (Math.Abs(distance - bestDistance) < 1e-12 && lineId.CompareTo(best?.LineId ?? Guid.Empty) < 0)))
            {
                bestDistance = distance;
                best = (point, lineId);
            }
        }
        return best;
    }

    private static Point3DValue Midpoint(Point3DValue a, Point3DValue b) =>
        new((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);

    private static double DistanceSquared(Point3DValue a, Point3DValue b)
    {
        var dx = a.X - b.X; var dy = a.Y - b.Y; var dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }
}

public enum PhysicalSnapKind { Grid, Endpoint, Midpoint, Intersection }
public sealed record PhysicalSnapResult(Point3DValue Position, PhysicalSnapKind Kind, Guid? EndpointNodeId)
{
    public Guid? GeometryObjectId { get; init; }
}
