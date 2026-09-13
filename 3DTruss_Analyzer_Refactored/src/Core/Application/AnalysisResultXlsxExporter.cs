namespace TrussAnalyzer.Core.Application;

using System.IO.Compression;
using System.Security;
using System.Text;
using TrussAnalyzer.Core.Models;

/// <summary>Minimal dependency-free Open XML workbook for deterministic result exchange.</summary>
public sealed class AnalysisResultXlsxExporter
{
    public void Save(AnalysisSnapshot snapshot, string path)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        Add(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
        Add(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Add(archive, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Results\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Add(archive, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
        Add(archive, "xl/worksheets/sheet1.xml", BuildSheet(snapshot));
    }

    private static string BuildSheet(AnalysisSnapshot snapshot)
    {
        var rows = new List<string> { Row("GOStructAnalysis Result Export v1", "", ""), Row("Document checksum", snapshot.DocumentChecksum, ""), Row("Solver", snapshot.SolverName, snapshot.SolverVersion), Row("Node ID", "DX (m)", "DY (m)", "DZ (m)") };
        rows.AddRange(snapshot.Nodes.OrderBy(n => n.NodeId).Select(n => Row(n.NodeId.ToString(), F(n.Displacement.X), F(n.Displacement.Y), F(n.Displacement.Z))));
        rows.Add(Row("Member ID", "Station", "N (N)", "Vy (N)", "Vz (N)", "T (N-m)", "My (N-m)", "Mz (N-m)"));
        rows.AddRange(snapshot.Members.OrderBy(m => m.LineObjectId).SelectMany(m => m.Result.StationResults.OrderBy(s => s.RelativePosition).Select(s => Row(m.LineObjectId.ToString(), F(s.RelativePosition), F(s.AxialForce), F(s.ShearY), F(s.ShearZ), F(s.Torsion), F(s.MomentY), F(s.MomentZ)))));
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" + string.Join("", rows.Select((row, i) => $"<row r=\"{i + 1}\">{row}</row>")) + "</sheetData></worksheet>";
    }

    private static string Row(params string[] values) => string.Join("", values.Select((value, i) => $"<c r=\"{Column(i)}\" t=\"inlineStr\"><is><t>{Escape(value)}</t></is></c>"));
    private static string Column(int index) => ((char)('A' + index)).ToString();
    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;
    private static string F(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    private static void Add(ZipArchive archive, string name, string content) { using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(content); }
}
