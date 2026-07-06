using System;
using System.Text;
using TrussAnalyzer.Core.Models;

namespace TrussAnalyzer.Core.Reporting
{
    /// <summary>
    /// Generates PDF reports for truss analysis results.
    /// Uses a simple PDF format without external dependencies.
    /// </summary>
    public class PdfReportGenerator
    {
        private readonly AnalysisResult _result;

        public PdfReportGenerator(AnalysisResult result)
        {
            _result = result ?? throw new ArgumentNullException(nameof(result));
        }

        /// <summary>
        /// Generates a PDF report and returns it as a byte array.
        /// </summary>
        public byte[] GenerateReport()
        {
            var pdfContent = BuildPdfContent();
            return Encoding.UTF8.GetBytes(pdfContent);
        }

        /// <summary>
        /// Saves the PDF report to a file.
        /// </summary>
        public void SaveToFile(string filePath)
        {
            var pdfBytes = GenerateReport();
            System.IO.File.WriteAllBytes(filePath, pdfBytes);
        }

        private string BuildPdfContent()
        {
            var sb = new StringBuilder();
            
            // PDF Header
            sb.AppendLine("%PDF-1.4");
            sb.AppendLine("1 0 obj");
            sb.AppendLine("<< /Type /Catalog /Pages 2 0 R >>");
            sb.AppendLine("endobj");
            
            // Pages
            sb.AppendLine("2 0 obj");
            sb.AppendLine("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
            sb.AppendLine("endobj");
            
            // Page content
            sb.AppendLine("3 0 obj");
            sb.AppendLine("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>");
            sb.AppendLine("endobj");
            
            // Content stream
            var contentStream = BuildContentStream();
            sb.AppendLine("4 0 obj");
            sb.AppendLine($"<< /Length {contentStream.Length} >>");
            sb.AppendLine("stream");
            sb.Append(contentStream);
            sb.AppendLine("endstream");
            sb.AppendLine("endobj");
            
            // Font
            sb.AppendLine("5 0 obj");
            sb.AppendLine("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            sb.AppendLine("endobj");
            
            // XRef table
            sb.AppendLine("xref");
            sb.AppendLine("0 6");
            sb.AppendLine("0000000000 65535 f ");
            sb.AppendLine("0000000009 00000 n ");
            sb.AppendLine("0000000058 00000 n ");
            sb.AppendLine("0000000115 00000 n ");
            sb.AppendLine("0000000262 00000 n ");
            sb.AppendLine("0000000" + contentStream.Length.ToString("D3") + " 00000 n ");
            
            // Trailer
            sb.AppendLine("trailer");
            sb.AppendLine("<< /Size 6 /Root 1 0 R >>");
            sb.AppendLine("startxref");
            sb.AppendLine("%%EOF");
            
            return sb.ToString();
        }

        private string BuildContentStream()
        {
            var sb = new StringBuilder();
            var snapshot = AnalysisReportSnapshot.FromAnalysisResult(_result);
            sb.AppendLine("BT");
            sb.AppendLine("/F1 16 Tf");
            sb.AppendLine("50 750 Td");
            AppendPdfLine(sb, snapshot.Title);
            
            sb.AppendLine("/F1 12 Tf");
            AppendPdfLine(sb, $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", -30);
            
            AppendPdfLine(sb, "Project Summary", -40);
            sb.AppendLine("/F1 10 Tf");
            AppendPdfLine(sb, $"Total Nodes: {snapshot.NodeCount}", -20);
            AppendPdfLine(sb, $"Total Elements: {snapshot.ElementCount}", -15);
            AppendPdfLine(sb, $"Load Case: {snapshot.LoadCaseName}", -15);
            AppendPdfLine(sb, $"Max Displacement: {snapshot.MaxDisplacement:E4} m", -15);

            AppendPdfLine(sb, "Design Criteria and Limitations", -30);
            foreach (string limitation in snapshot.Limitations)
                AppendPdfLine(sb, limitation, -15);

            AppendPdfLine(sb, "Member Force Envelope", -30);
            AppendPdfLine(sb, "Units: axial force=N, stress=MPa, utilization=ratio", -15);
            foreach (var row in snapshot.MemberForceEnvelope.Take(6))
            {
                AppendPdfLine(sb, $"Element {row.ElementId}: N={row.AxialForce:F2}, Stress={row.Stress / 1e6:F3}, Util={row.Utilization:F3}", -15);
            }
            
            AppendPdfLine(sb, "Node Displacements", -30);
            sb.AppendLine("/F1 8 Tf");
            double y = 520;
            foreach (var node in _result.Nodes)
            {
                var disp = node.Displacement;
                AppendPdfLine(sb, $"Node {node.Id}: DX={disp.X:E4}, DY={disp.Y:E4}, DZ={disp.Z:E4}", -15);
                y -= 15;
                if (y < 200) break; // Limit displayed nodes
            }
            
            sb.AppendLine("/F1 12 Tf");
            AppendPdfLine(sb, "Element Forces", -30);
            sb.AppendLine("/F1 8 Tf");
            y = 350;
            foreach (var elem in _result.Elements)
            {
                string state = elem.AxialForce > 0 ? "Tension" : (elem.AxialForce < 0 ? "Compression" : "Zero");
                AppendPdfLine(sb, $"Element {elem.Id}: {elem.AxialForce:F2} N, {elem.Stress / 1e6:F3} MPa [{state}]", -15);
                y -= 15;
                if (y < 150) break; // Limit displayed elements
            }
            
            sb.AppendLine("/F1 12 Tf");
            AppendPdfLine(sb, "Support Reactions", -30);
            sb.AppendLine("/F1 8 Tf");
            foreach (var node in _result.Nodes.Where(n => n.IsConstrained))
            {
                var reaction = node.ReactionForce;
                AppendPdfLine(sb, $"Node {node.Id}: RX={reaction.X:F2}, RY={reaction.Y:F2}, RZ={reaction.Z:F2}", -15);
            }
            
            sb.AppendLine("/F1 10 Tf");
            AppendPdfLine(sb, "Equilibrium Check", -40);
            sb.AppendLine("/F1 8 Tf");
            AppendPdfLine(sb, $"Sum FX: {_result.Equilibrium.SumFX:E6} N", -15);
            AppendPdfLine(sb, $"Sum FY: {_result.Equilibrium.SumFY:E6} N", -15);
            AppendPdfLine(sb, $"Sum FZ: {_result.Equilibrium.SumFZ:E6} N", -15);

            sb.AppendLine("/F1 12 Tf");
            AppendPdfLine(sb, "Safety Checks", -30);
            sb.AppendLine("/F1 8 Tf");
            foreach (var check in _result.SafetyChecks.ElementChecks.Take(10))
            {
                AppendPdfLine(sb, $"Element {check.ElementId}: Util={check.UtilizationRatio:F3}, {check.Status}", -15);
            }
            
            sb.AppendLine("ET");
            
            return sb.ToString();
        }

        private static void AppendPdfLine(StringBuilder sb, string text, int yOffset = 0)
        {
            if (yOffset != 0)
                sb.AppendLine($"0 {yOffset} Td");
            sb.AppendLine($"({EscapePdfText(text)}) Tj");
        }

        private static string EscapePdfText(string text)
        {
            return text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }
    }
}
