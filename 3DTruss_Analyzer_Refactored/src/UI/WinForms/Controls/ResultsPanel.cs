using System;
using System.Windows.Forms;
using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.IO;
using TrussAnalyzer.Core.Reporting;

namespace TrussAnalyzer.UI.WinForms.Controls
{
    public partial class ResultsPanel : UserControl
    {
        private AnalysisResult? _currentResult;

        public ResultsPanel()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.AutoScroll = true;
        }

        public void DisplayResults(AnalysisResult result)
        {
            _currentResult = result;

            // Clear existing controls
            this.Controls.Clear();

            int yOffset = 20;
            int xOffset = 20;
            int lineHeight = 25;

            // Title
            var lblTitle = new Label
            {
                Text = "Analysis Results",
                Font = new System.Drawing.Font("Arial", 16, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(xOffset, yOffset),
                AutoSize = true
            };
            this.Controls.Add(lblTitle);
            yOffset += 40;

            // Summary
            var lblSummary = new Label
            {
                Text = $"Nodes: {result.Nodes.Count} | Elements: {result.Elements.Count} | Supports: {result.Nodes.Count(n => n.IsConstrained)}",
                Font = new System.Drawing.Font("Arial", 10),
                Location = new System.Drawing.Point(xOffset, yOffset),
                AutoSize = true
            };
            this.Controls.Add(lblSummary);
            yOffset += 30;

            // Equilibrium Check
            var lblEquilibrium = new Label
            {
                Text = $"Equilibrium Check: FX={result.Equilibrium.SumFX:E6}, FY={result.Equilibrium.SumFY:E6}, FZ={result.Equilibrium.SumFZ:E6}",
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Italic),
                Location = new System.Drawing.Point(xOffset, yOffset),
                AutoSize = true,
                ForeColor = result.EquilibriumSatisfied ? 
                           System.Drawing.Color.Green : System.Drawing.Color.Red
            };
            this.Controls.Add(lblEquilibrium);
            yOffset += 30;

            // Displacements Section
            yOffset += 20;
            var lblDisplacements = new Label
            {
                Text = "Node Displacements (m)",
                Font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(xOffset, yOffset),
                AutoSize = true
            };
            this.Controls.Add(lblDisplacements);
            yOffset += 25;

            foreach (var node in result.Nodes)
            {
                var disp = node.Displacement;
                var lblDisp = new Label
                {
                    Text = $"Node {node.Id}: DX={disp.X:E6}, DY={disp.Y:E6}, DZ={disp.Z:E6}",
                    Font = new System.Drawing.Font("Courier New", 9),
                    Location = new System.Drawing.Point(xOffset + 20, yOffset),
                    AutoSize = true
                };
                this.Controls.Add(lblDisp);
                yOffset += lineHeight;
            }

            // Element Forces Section
            yOffset += 20;
            var lblForces = new Label
            {
                Text = "Element Forces (N)",
                Font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(xOffset, yOffset),
                AutoSize = true
            };
            this.Controls.Add(lblForces);
            yOffset += 25;

            foreach (var elem in result.Elements)
            {
                string state = elem.AxialForce > 0 ? "Tension" : (elem.AxialForce < 0 ? "Compression" : "Zero");
                System.Drawing.Color color = elem.AxialForce > 0 ? System.Drawing.Color.Blue : 
                                              (elem.AxialForce < 0 ? System.Drawing.Color.Red : System.Drawing.Color.Gray);
                    
                var lblForce = new Label
                {
                    Text = $"Element {elem.Id}: {elem.AxialForce:F2} N [{state}]",
                    Font = new System.Drawing.Font("Courier New", 9),
                    Location = new System.Drawing.Point(xOffset + 20, yOffset),
                    AutoSize = true,
                    ForeColor = color
                };
                this.Controls.Add(lblForce);
                yOffset += lineHeight;
            }

            // Reactions Section
            yOffset += 20;
            var lblReactions = new Label
            {
                Text = "Support Reactions (N)",
                Font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(xOffset, yOffset),
                AutoSize = true
            };
            this.Controls.Add(lblReactions);
            yOffset += 25;

            foreach (var node in result.Nodes.Where(n => n.IsConstrained))
            {
                var reaction = node.ReactionForce;
                var lblReaction = new Label
                {
                    Text = $"Node {node.Id}: RX={reaction.X:F2}, RY={reaction.Y:F2}, RZ={reaction.Z:F2}",
                    Font = new System.Drawing.Font("Courier New", 9),
                    Location = new System.Drawing.Point(xOffset + 20, yOffset),
                    AutoSize = true
                };
                this.Controls.Add(lblReaction);
                yOffset += lineHeight;
            }

            // Export Buttons
            yOffset += 30;
            var btnExportPdf = new Button
            {
                Text = "Export PDF Report",
                Location = new System.Drawing.Point(xOffset, yOffset),
                Size = new System.Drawing.Size(150, 30)
            };
            btnExportPdf.Click += BtnExportPdf_Click;
            this.Controls.Add(btnExportPdf);

            var btnExportCsv = new Button
            {
                Text = "Export CSV",
                Location = new System.Drawing.Point(xOffset + 160, yOffset),
                Size = new System.Drawing.Size(150, 30)
            };
            btnExportCsv.Click += BtnExportCsv_Click;
            this.Controls.Add(btnExportCsv);
        }

        private void BtnExportPdf_Click(object? sender, EventArgs e)
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "PDF Files|*.pdf";
                saveDialog.Title = "Save PDF Report";
                saveDialog.FileName = "TrussAnalysisReport.pdf";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (_currentResult == null)
                            throw new InvalidOperationException("No analysis result is available.");

                        var pdfGenerator = new PdfReportGenerator(_currentResult);
                        pdfGenerator.SaveToFile(saveDialog.FileName);
                        MessageBox.Show($"PDF report saved successfully to:\n{saveDialog.FileName}", 
                                      "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving PDF: {ex.Message}", "Error", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnExportCsv_Click(object? sender, EventArgs e)
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "CSV Files|*.csv";
                saveDialog.Title = "Save CSV Results";
                saveDialog.FileName = "TrussResults.csv";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (_currentResult == null)
                            throw new InvalidOperationException("No analysis result is available.");

                        StructureImporterExporter.ExportResultsToCsv(_currentResult, saveDialog.FileName);
                        MessageBox.Show($"CSV results saved successfully to:\n{saveDialog.FileName}", 
                                      "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving CSV: {ex.Message}", "Error", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}
