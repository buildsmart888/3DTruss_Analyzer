namespace TrussAnalyzer.UI.WinForms;

using System;
using System.Windows.Forms;
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;

/// <summary>
/// Main form for 3D Truss Analyzer application.
/// Provides UI for creating, analyzing, and viewing truss structures.
/// </summary>
public partial class MainForm : Form
{
    private readonly TrussSolver _solver = new();
    private DataGridView? dgvNodes;
    private DataGridView? dgvElements;
    private DataGridView? dgvResults;
    private TextBox? txtStatus;
    
    public MainForm()
    {
        InitializeComponent();
        LoadSampleStructure();
    }
    
    private void InitializeComponent()
    {
        this.Text = "3D Truss Analyzer - Engineering Edition";
        this.Size = new System.Drawing.Size(1200, 800);
        this.StartPosition = FormStartPosition.CenterScreen;
        
        // Create menu strip
        var menuStrip = new MenuStrip();
        
        var fileMenu = new ToolStripMenuItem("File");
        var newProject = new ToolStripMenuItem("New Project");
        newProject.Click += (s, e) => NewProject();
        var openProject = new ToolStripMenuItem("Open Project");
        openProject.Click += (s, e) => OpenProject();
        var saveProject = new ToolStripMenuItem("Save Project");
        saveProject.Click += (s, e) => SaveProject();
        var exportReport = new ToolStripMenuItem("Export Report (PDF/Excel)");
        exportReport.Click += (s, e) => ExportReport();
        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (s, e) => Close();
        
        fileMenu.DropDownItems.AddRange(new[] { newProject, openProject, saveProject, exportReport, new ToolStripSeparator(), exit });
        
        var analyzeMenu = new ToolStripMenuItem("Analyze");
        var runAnalysis = new ToolStripMenuItem("Run Analysis");
        runAnalysis.Click += (s, e) => RunAnalysis();
        var checkEquilibrium = new ToolStripMenuItem("Check Equilibrium");
        checkEquilibrium.Click += (s, e) => CheckEquilibrium();
        
        analyzeMenu.DropDownItems.AddRange(new[] { runAnalysis, checkEquilibrium });
        
        var helpMenu = new ToolStripMenuItem("Help");
        var about = new ToolStripMenuItem("About");
        about.Click += (s, e) => ShowAbout();
        helpMenu.DropDownItems.Add(about);
        
        menuStrip.Items.AddRange(new[] { fileMenu, analyzeMenu, helpMenu });
        this.MainMenuStrip = menuStrip;
        this.Controls.Add(menuStrip);
        
        // Create main panel with tab control
        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Location = new System.Drawing.Point(0, 24)
        };
        
        // Input tab
        var tabInput = new TabPage("Input Data");
        CreateInputTab(tabInput);
        
        // Results tab
        var tabResults = new TabPage("Analysis Results");
        CreateResultsTab(tabResults);
        
        // 3D View tab (placeholder)
        var tabView3D = new TabPage("3D Visualization");
        Create3DViewTab(tabView3D);
        
        tabControl.TabPages.AddRange(new[] { tabInput, tabResults, tabView3D });
        this.Controls.Add(tabControl);
        
        // Status bar
        var statusStrip = new StatusStrip();
        txtStatus = new TextBox
        {
            Name = "txtStatus",
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Height = 60,
            Dock = DockStyle.Bottom
        };
        this.Controls.Add(txtStatus);
        
        UpdateStatus("Ready. Load or create a truss structure to begin.");
    }
    
    private void CreateInputTab(TabPage tab)
    {
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        
        // Nodes grid
        var grpNodes = new GroupBox { Text = "Nodes", Dock = DockStyle.Fill };
        dgvNodes = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true
        };
        
        // Define columns
        dgvNodes.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID" });
        dgvNodes.Columns.Add(new DataGridViewTextBoxColumn { Name = "X", HeaderText = "X (m)" });
        dgvNodes.Columns.Add(new DataGridViewTextBoxColumn { Name = "Y", HeaderText = "Y (m)" });
        dgvNodes.Columns.Add(new DataGridViewTextBoxColumn { Name = "Z", HeaderText = "Z (m)" });
        dgvNodes.Columns.Add(new DataGridViewCheckBoxColumn { Name = "CX", HeaderText = "Fix X" });
        dgvNodes.Columns.Add(new DataGridViewCheckBoxColumn { Name = "CY", HeaderText = "Fix Y" });
        dgvNodes.Columns.Add(new DataGridViewCheckBoxColumn { Name = "CZ", HeaderText = "Fix Z" });
        dgvNodes.Columns.Add(new DataGridViewTextBoxColumn { Name = "FX", HeaderText = "Force X (N)" });
        dgvNodes.Columns.Add(new DataGridViewTextBoxColumn { Name = "FY", HeaderText = "Force Y (N)" });
        dgvNodes.Columns.Add(new DataGridViewTextBoxColumn { Name = "FZ", HeaderText = "Force Z (N)" });
        
        grpNodes.Controls.Add(dgvNodes);
        splitContainer.Panel1.Controls.Add(grpNodes);
        
        // Elements grid
        var grpElements = new GroupBox { Text = "Elements", Dock = DockStyle.Fill };
        dgvElements = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true
        };
        
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "StartNode", HeaderText = "Start Node" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndNode", HeaderText = "End Node" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "Area", HeaderText = "Area (m²)" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "EModulus", HeaderText = "E (Pa)" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "Density", HeaderText = "Density (kg/m³)" });
        
        grpElements.Controls.Add(dgvElements);
        splitContainer.Panel2.Controls.Add(grpElements);
        
        tab.Controls.Add(splitContainer);
        
        // Buttons
        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft
        };
        
        var btnAddNode = new Button { Text = "Add Node", Width = 100, Margin = new Padding(5) };
        btnAddNode.Click += (s, e) => AddNode();
        
        var btnAddElement = new Button { Text = "Add Element", Width = 100, Margin = new Padding(5) };
        btnAddElement.Click += (s, e) => AddElement();
        
        var btnLoadSample = new Button { Text = "Load Sample", Width = 100, Margin = new Padding(5) };
        btnLoadSample.Click += (s, e) => LoadSampleStructure();
        
        pnlButtons.Controls.AddRange(new Control[] { btnLoadSample, btnAddElement, btnAddNode });
        tab.Controls.Add(pnlButtons);
    }
    
    private void CreateResultsTab(TabPage tab)
    {
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        
        // Displacements
        var grpDisplacements = new GroupBox { Text = "Node Displacements", Dock = DockStyle.Fill };
        dgvResults = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = true
        };
        
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "NodeId", HeaderText = "Node ID" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Dx", HeaderText = "δX (mm)" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Dy", HeaderText = "δY (mm)" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Dz", HeaderText = "δZ (mm)" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rx", HeaderText = "Reaction X (N)" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ry", HeaderText = "Reaction Y (N)" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rz", HeaderText = "Reaction Z (N)" });
        
        grpDisplacements.Controls.Add(dgvResults);
        splitContainer.Panel1.Controls.Add(grpDisplacements);
        
        // Element forces
        var grpForces = new GroupBox { Text = "Element Forces & Stresses", Dock = DockStyle.Fill };
        var dgvForces = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = true
        };
        
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "ElemId", HeaderText = "Element ID" });
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "Force", HeaderText = "Axial Force (N)" });
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "Stress", HeaderText = "Stress (MPa)" });
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "Strain", HeaderText = "Strain" });
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "Length", HeaderText = "Length (m)" });
        
        grpForces.Controls.Add(dgvForces);
        splitContainer.Panel2.Controls.Add(grpForces);
        
        tab.Controls.Add(splitContainer);
    }
    
    private void Create3DViewTab(TabPage tab)
    {
        var lblPlaceholder = new Label
        {
            Text = "3D Visualization Module\n\nThis feature will display an interactive 3D model of the truss structure.\n\nPlanned features:\n- Rotate, pan, zoom\n- Color-coded stress/displacement visualization\n- Animation of deformation\n- Export to CAD formats",
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Font = new System.Drawing.Font("Segoe UI", 12f)
        };
        tab.Controls.Add(lblPlaceholder);
    }
    
    private void LoadSampleStructure()
    {
        _solver = new TrussSolver();
        
        // Simple tripod example
        var material = Material.StructuralSteel;
        double area = 0.002;
        
        var baseNodes = new[]
        {
            new Node(1, new Point3D(2, 0, 0)),
            new Node(2, new Point3D(-1, 1.732, 0)),
            new Node(3, new Point3D(-1, -1.732, 0))
        };
        
        foreach (var node in baseNodes)
        {
            node.ConstraintX = true;
            node.ConstraintY = true;
            node.ConstraintZ = true;
            _solver.AddNode(node);
        }
        
        var topNode = new Node(4, new Point3D(0, 0, 3));
        topNode.ApplyForce(0, 0, -5000);
        _solver.AddNode(topNode);
        
        for (int i = 0; i < 3; i++)
        {
            _solver.AddElement(new Element(i + 1, i + 1, 4, area, material));
        }
        
        PopulateGrids();
        UpdateStatus("Sample tripod structure loaded. Click 'Run Analysis' to calculate results.");
    }
    
    private void PopulateGrids()
    {
        if (dgvNodes == null || dgvElements == null) return;
        
        dgvNodes.Rows.Clear();
        foreach (var node in _solver.GetNodes())
        {
            dgvNodes.Rows.Add(
                node.Id, node.Position.X, node.Position.Y, node.Position.Z,
                node.ConstraintX, node.ConstraintY, node.ConstraintZ,
                node.AppliedForce.X, node.AppliedForce.Y, node.AppliedForce.Z
            );
        }
        
        dgvElements.Rows.Clear();
        foreach (var elem in _solver.GetElements())
        {
            dgvElements.Rows.Add(
                elem.Id, elem.StartNodeId, elem.EndNodeId,
                elem.CrossSectionalArea, elem.Material.YoungsModulus, elem.Material.Density
            );
        }
    }
    
    private void RunAnalysis()
    {
        try
        {
            UpdateStatus("Running analysis...");
            var result = _solver.Analyze();
            
            if (result.EquilibriumSatisfied)
            {
                UpdateStatus("✓ Analysis completed successfully. Equilibrium satisfied.");
                DisplayResults(result);
            }
            else
            {
                UpdateStatus("⚠ Analysis completed but equilibrium NOT satisfied. Check constraints and loads.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Analysis failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateStatus($"Analysis failed: {ex.Message}");
        }
    }
    
    private void DisplayResults(AnalysisResult result)
    {
        if (dgvResults == null) return;
        
        dgvResults.Rows.Clear();
        foreach (var node in result.Nodes)
        {
            dgvResults.Rows.Add(
                node.Id,
                (node.Displacement.X * 1000).ToString("F4"),
                (node.Displacement.Y * 1000).ToString("F4"),
                (node.Displacement.Z * 1000).ToString("F4"),
                node.ReactionForce.X.ToString("F2"),
                node.ReactionForce.Y.ToString("F2"),
                node.ReactionForce.Z.ToString("F2")
            );
        }
        
        // Switch to results tab
        var tabControl = this.Controls.OfType<TabControl>().FirstOrDefault();
        if (tabControl != null)
        {
            tabControl.SelectedIndex = 1;
        }
    }
    
    private void CheckEquilibrium()
    {
        if (_solver.LastResult == null)
        {
            MessageBox.Show("Please run analysis first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        
        string msg = _solver.LastResult.EquilibriumSatisfied 
            ? "✓ Structure is in equilibrium." 
            : "⚠ Equilibrium NOT satisfied!";
        MessageBox.Show(msg, "Equilibrium Check", MessageBoxButtons.OK, 
            _solver.LastResult.EquilibriumSatisfied ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }
    
    private void NewProject() => _solver = new TrussSolver();
    private void OpenProject() => MessageBox.Show("Open Project - To be implemented", "Info");
    private void SaveProject() => MessageBox.Show("Save Project - To be implemented", "Info");
    
    private void ExportReport()
    {
        if (_solver.LastResult == null)
        {
            MessageBox.Show("Please run analysis first.", "Info");
            return;
        }
        
        var dlg = new SaveFileDialog
        {
            Filter = "Text File|*.txt|CSV File|*.csv",
            Title = "Export Analysis Report"
        };
        
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            ExportToTextFile(dlg.FileName);
            MessageBox.Show($"Report exported to {dlg.FileName}", "Success");
        }
    }
    
    private void ExportToTextFile(string filePath)
    {
        using var writer = new StreamWriter(filePath);
        writer.WriteLine("=== 3D Truss Analysis Report ===");
        writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine();
        
        var result = _solver.LastResult!;
        writer.WriteLine("--- Summary ---");
        writer.WriteLine($"Equilibrium: {(result.EquilibriumSatisfied ? "Satisfied" : "NOT Satisfied")}");
        writer.WriteLine($"Max Displacement: {result.MaxDisplacement:E4} m");
        writer.WriteLine($"Max Axial Force: {result.MaxAxialForce:E2} N");
        writer.WriteLine($"Max Stress: {result.MaxStress:E2} Pa");
        writer.WriteLine();
        
        writer.WriteLine("--- Node Results ---");
        writer.WriteLine("ID\tδX(mm)\tδY(mm)\tδZ(mm)\tRX(N)\tRY(N)\tRZ(N)");
        foreach (var node in result.Nodes)
        {
            writer.WriteLine($"{node.Id}\t{node.Displacement.X*1000:F3}\t{node.Displacement.Y*1000:F3}\t{node.Displacement.Z*1000:F3}\t{node.ReactionForce.X:F2}\t{node.ReactionForce.Y:F2}\t{node.ReactionForce.Z:F2}");
        }
        writer.WriteLine();
        
        writer.WriteLine("--- Element Results ---");
        writer.WriteLine("ID\tForce(N)\tStress(MPa)\tLength(m)");
        foreach (var elem in result.Elements)
        {
            writer.WriteLine($"{elem.Id}\t{elem.AxialForce:F2}\t{elem.Stress/1e6:F3}\t{elem.Length:F3}");
        }
    }
    
    private void ShowAbout()
    {
        MessageBox.Show(
            "3D Truss Analyzer v1.0\n\n" +
            "Engineering-grade structural analysis software\n" +
            "Built on Finite Element Method (FEM)\n\n" +
            "Features:\n" +
            "• 3D truss analysis\n" +
            "• Self-weight calculation\n" +
            "• Equilibrium verification\n" +
            "• Export reports\n\n" +
            "© 2024 - Open Source",
            "About",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }
    
    private void AddNode()
    {
        // Placeholder - would open dialog to add node
        MessageBox.Show("Add Node dialog - To be implemented", "Info");
    }
    
    private void AddElement()
    {
        // Placeholder - would open dialog to add element
        MessageBox.Show("Add Element dialog - To be implemented", "Info");
    }
    
    private void UpdateStatus(string message)
    {
        if (txtStatus != null)
        {
            txtStatus.Text = message;
        }
    }
}
