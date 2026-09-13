namespace TrussAnalyzer.Core.Application;

using System.Globalization;
using System.Text;
using System.Text.Json;

/// <summary>Single formatting contract shared by result explorer, reports and machine exports.</summary>
public sealed class AnalysisResultExportService
{
    public string ToJson(AnalysisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
    }

    public string ToCsv(AnalysisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var sb = new StringBuilder();
        sb.AppendLine("GOStructAnalysis Result Export v1");
        sb.AppendLine($"Selection,{snapshot.SelectionKind},{snapshot.SelectionId}");
        sb.AppendLine($"DocumentChecksum,{snapshot.DocumentChecksum}");
        sb.AppendLine($"Solver,{snapshot.SolverName} {snapshot.SolverVersion}");
        sb.AppendLine("NodeId,DX (m),DY (m),DZ (m),RX (rad),RY (rad),RZ (rad),ReactionX (N),ReactionY (N),ReactionZ (N),ReactionMX (N-m),ReactionMY (N-m),ReactionMZ (N-m)");
        foreach (var node in snapshot.Nodes.OrderBy(n => n.NodeId))
            sb.AppendLine(string.Join(',', node.NodeId, F(node.Displacement.X), F(node.Displacement.Y), F(node.Displacement.Z), F(node.Rotation.X), F(node.Rotation.Y), F(node.Rotation.Z), F(node.ReactionForce.X), F(node.ReactionForce.Y), F(node.ReactionForce.Z), F(node.ReactionMoment.X), F(node.ReactionMoment.Y), F(node.ReactionMoment.Z)));
        sb.AppendLine("MemberId,Station,Side,N (N),Vy (N),Vz (N),T (N-m),My (N-m),Mz (N-m)");
        foreach (var member in snapshot.Members.OrderBy(m => m.LineObjectId))
            foreach (var station in member.Result.StationResults.OrderBy(s => s.RelativePosition))
                sb.AppendLine(string.Join(',', member.LineObjectId, F(station.RelativePosition), station.DiagramSide, F(station.AxialForce), F(station.ShearY), F(station.ShearZ), F(station.Torsion), F(station.MomentY), F(station.MomentZ)));
        return sb.ToString();
    }

    private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
