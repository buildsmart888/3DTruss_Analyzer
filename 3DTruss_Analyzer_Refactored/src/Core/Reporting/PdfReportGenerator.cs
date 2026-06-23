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
        private readonly StructureModel _model;

        public PdfReportGenerator(StructureModel model, AnalysisResult result)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
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
            sb.AppendLine("BT");
            sb.AppendLine("/F1 16 Tf");
            sb.AppendLine("50 750 Td");
            sb.AppendLine("(3D Truss Analysis Report) Tj");
            
            sb.AppendLine("/F1 12 Tf");
            sb.AppendLine("0 -30 Td");
            sb.AppendLine($"(Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}) Tj");
            
            sb.AppendLine("0 -40 Td");
            sb.AppendLine("(Project Summary) Tj");
            sb.AppendLine("/F1 10 Tf");
            sb.AppendLine("0 -20 Td");
            sb.AppendLine($"(Total Nodes: {_model.Nodes.Count}) Tj");
            sb.AppendLine("0 -15 Td");
            sb.AppendLine($"(Total Elements: {_model.Elements.Count}) Tj");
            sb.AppendLine("0 -15 Td");
            sb.AppendLine($"(Total Load Cases: 1) Tj");
            
            sb.AppendLine("0 -30 Td");
            sb.AppendLine("(Node Displacements) Tj");
            sb.AppendLine("/F1 8 Tf");
            double y = 520;
            foreach (var node in _model.Nodes)
            {
                if (_result.NodeDisplacements.TryGetValue(node.Id, out var disp))
                {
                    sb.AppendLine($"0 -15 Td");
                    sb.AppendLine($"(Node {node.Id}: DX={disp.X:E4}, DY={disp.Y:E4}, DZ={disp.Z:E4}) Tj");
                    y -= 15;
                    if (y < 200) break; // Limit displayed nodes
                }
            }
            
            sb.AppendLine("0 -30 Td");
            sb.AppendLine("/F1 12 Tf");
            sb.AppendLine("(Element Forces) Tj");
            sb.AppendLine("/F1 8 Tf");
            y = 350;
            foreach (var elem in _model.Elements)
            {
                if (_result.ElementForces.TryGetValue(elem.Id, out var force))
                {
                    string state = force > 0 ? "Tension" : (force < 0 ? "Compression" : "Zero");
                    sb.AppendLine($"0 -15 Td");
                    sb.AppendLine($"(Element {elem.Id}: {force:F2} N [{state}]) Tj");
                    y -= 15;
                    if (y < 150) break; // Limit displayed elements
                }
            }
            
            sb.AppendLine("0 -30 Td");
            sb.AppendLine("/F1 12 Tf");
            sb.AppendLine("(Support Reactions) Tj");
            sb.AppendLine("/F1 8 Tf");
            foreach (var constraint in _model.Constraints)
            {
                if (_result.Reactions.TryGetValue(constraint.NodeId, out var reaction))
                {
                    sb.AppendLine($"0 -15 Td");
                    sb.AppendLine($"(Node {constraint.NodeId}: RX={reaction.X:F2}, RY={reaction.Y:F2}, RZ={reaction.Z:F2}) Tj");
                }
            }
            
            sb.AppendLine("0 -40 Td");
            sb.AppendLine("/F1 10 Tf");
            sb.AppendLine("(Equilibrium Check) Tj");
            sb.AppendLine("/F1 8 Tf");
            sb.AppendLine($"0 -15 Td");
            sb.AppendLine($"(Sum FX: {_result.EquilibriumCheck.SumFX:E6} N) Tj");
            sb.AppendLine("0 -15 Td");
            sb.AppendLine($"(Sum FY: {_result.EquilibriumCheck.SumFY:E6} N) Tj");
            sb.AppendLine("0 -15 Td");
            sb.AppendLine($"(Sum FZ: {_result.EquilibriumCheck.SumFZ:E6} N) Tj");
            
            sb.AppendLine("ET");
            
            return sb.ToString();
        }
    }
}
