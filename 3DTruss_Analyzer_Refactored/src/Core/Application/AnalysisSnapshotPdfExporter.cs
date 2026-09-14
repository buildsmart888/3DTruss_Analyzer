namespace TrussAnalyzer.Core.Application;

using System.Globalization;
using System.Text;

/// <summary>Dependency-free, deterministic one-page PDF export of the selected immutable result snapshot.</summary>
public sealed class AnalysisSnapshotPdfExporter
{
    public byte[] Generate(AnalysisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string content = BuildContent(snapshot);
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        using var stream = new MemoryStream();
        Write(stream, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (int index = 0; index < objects.Length; index++)
        {
            offsets.Add(stream.Position);
            Write(stream, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        long xrefOffset = stream.Position;
        Write(stream, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1))
            Write(stream, $"{offset:0000000000} 00000 n \n");
        Write(stream, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return stream.ToArray();
    }

    public void Save(AnalysisSnapshot snapshot, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(path, Generate(snapshot));
    }

    private static string BuildContent(AnalysisSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "GOStructAnalysis Result Export v1",
            $"Selection: {snapshot.SelectionKind} {snapshot.SelectionId}",
            $"Document checksum: {snapshot.DocumentChecksum}",
            $"Solver: {snapshot.SolverName} {snapshot.SolverVersion}",
            $"Maximum displacement: {F(snapshot.MaxDisplacement)} m",
            $"Maximum utilization: {F(snapshot.MaxUtilization)}",
            $"Equilibrium satisfied: {snapshot.Equilibrium.IsSatisfied}",
            "Node results: displacement=m, rotation=rad, reaction=N/N-m"
        };
        lines.AddRange(snapshot.Nodes.OrderBy(value => value.NodeId).Take(12).Select(value =>
            $"Node {value.NodeId}: U=({F(value.Displacement.X)}, {F(value.Displacement.Y)}, {F(value.Displacement.Z)}) R=({F(value.ReactionForce.X)}, {F(value.ReactionForce.Y)}, {F(value.ReactionForce.Z)})"));
        lines.Add("Member station results: N/V=N, T/M=N-m; native solver signs");
        lines.AddRange(snapshot.Members.OrderBy(value => value.LineObjectId)
            .SelectMany(member => member.Result.StationResults.OrderBy(value => value.RelativePosition).ThenBy(value => value.DiagramSide)
                .Select(station => $"Member {member.LineObjectId} x/L={F(station.RelativePosition)} {station.DiagramSide}: N={F(station.AxialForce)} Vy={F(station.ShearY)} Vz={F(station.ShearZ)} T={F(station.Torsion)} My={F(station.MomentY)} Mz={F(station.MomentZ)}"))
            .Take(Math.Max(0, 42 - lines.Count)));

        var content = new StringBuilder("BT\n/F1 9 Tf\n");
        for (int index = 0; index < lines.Count && index < 42; index++)
            content.Append($"1 0 0 1 36 {756 - index * 17} Tm ({Escape(lines[index])}) Tj\n");
        content.Append("ET\n");
        return content.ToString();
    }

    private static void Write(Stream stream, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Escape(string value) => string.Concat(value.Select(character => character is >= ' ' and <= '~' ? character : '?'))
        .Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
