namespace TrussAnalyzer.UI.WinForms;

using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.IO;
using TrussAnalyzer.Core.Models;
using TrussAnalyzer.UI.WinForms.Controls;

/// <summary>
/// Main form for 3D Truss Analyzer application.
/// Provides UI for creating, analyzing, and viewing truss structures.
/// </summary>
public partial class MainForm : Form
{
    private TrussSolver _solver = new();
    private DataGridView? dgvNodes;
    private DataGridView? dgvElements;
    private DataGridView? dgvMaterials;
    private DataGridView? dgvSections;
    private DataGridView? dgvLoads;
    private DataGridView? dgvCombinations;
    private DataGridView? dgvValidation;
    private DataGridView? dgvResults;
    private DataGridView? dgvForces;
    private DataGridView? dgvStations;
    private DataGridView? dgvDesignChecks;
    private HelixStructuralView? glView;
    private TabControl? mainTabs;
    private TreeView? objectTree;
    private PropertyGrid? propertyGrid;
    private ToolStripComboBox? activeLoadCaseCombo;
    private ToolStripComboBox? activeCombinationCombo;
    private TextBox? txtStatus;
    private StructuralModel? _structuralModel;
    private StructuralAnalysisResult? _structuralResult;
    private bool _syncingSelection;
    
    public MainForm()
    {
        InitializeComponent();
        LoadSampleStructure();
    }
    
    private void InitializeComponent()
    {
        this.Text = "3D Truss Analyzer - Engineering Edition";
        this.Size = new System.Drawing.Size(1500, 950);
        this.StartPosition = FormStartPosition.CenterScreen;
        
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        this.Controls.Add(layout);

        var menuStrip = new MenuStrip { Dock = DockStyle.Fill };
        
        var fileMenu = new ToolStripMenuItem("File");
        var newProject = new ToolStripMenuItem("New Project");
        newProject.Click += (s, e) => NewProject();
        var openProject = new ToolStripMenuItem("Open Project");
        openProject.Click += (s, e) => OpenProject();
        var saveProject = new ToolStripMenuItem("Save Project");
        saveProject.Click += (s, e) => SaveProject();
        var exportReport = new ToolStripMenuItem("Export Report (Text/CSV/PDF)");
        exportReport.Click += (s, e) => ExportReport();
        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (s, e) => Close();
        
        fileMenu.DropDownItems.AddRange(new ToolStripItem[] { newProject, openProject, saveProject, exportReport, new ToolStripSeparator(), exit });
        
        var analyzeMenu = new ToolStripMenuItem("Analyze");
        var runAnalysis = new ToolStripMenuItem("Run Analysis");
        runAnalysis.Click += (s, e) => RunAnalysis();
        var show3D = new ToolStripMenuItem("Show 3D View");
        show3D.Click += (s, e) => Show3DView();
        var checkEquilibrium = new ToolStripMenuItem("Check Equilibrium");
        checkEquilibrium.Click += (s, e) => CheckEquilibrium();
        var validateModel = new ToolStripMenuItem("Validate Model");
        validateModel.Click += (s, e) => ValidateCurrentModel(showDialog: true);
        
        analyzeMenu.DropDownItems.AddRange(new[] { runAnalysis, show3D, validateModel, checkEquilibrium });
        
        var helpMenu = new ToolStripMenuItem("Help");
        var about = new ToolStripMenuItem("About");
        about.Click += (s, e) => ShowAbout();
        helpMenu.DropDownItems.Add(about);
        
        menuStrip.Items.AddRange(new[] { fileMenu, analyzeMenu, helpMenu });
        this.MainMenuStrip = menuStrip;
        layout.Controls.Add(menuStrip, 0, 0);

        var ribbon = CreateRibbon();
        layout.Controls.Add(ribbon, 0, 1);

        var selectorStrip = CreateSelectorStrip();
        layout.Controls.Add(selectorStrip, 0, 2);

        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 260,
            FixedPanel = FixedPanel.Panel1
        };
        layout.Controls.Add(mainSplit, 0, 3);

        objectTree = new TreeView
        {
            Dock = DockStyle.Fill,
            CheckBoxes = true,
            HideSelection = false
        };
        objectTree.AfterCheck += ObjectTreeAfterCheck;
        objectTree.AfterSelect += (_, e) => OnObjectTreeSelected(e.Node);
        mainSplit.Panel1.Controls.Add(objectTree);

        var rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 980
        };
        mainSplit.Panel2.Controls.Add(rightSplit);

        var centerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 520
        };
        rightSplit.Panel1.Controls.Add(centerSplit);

        glView = new HelixStructuralView { Dock = DockStyle.Fill };
        glView.ObjectSelected += (_, selection) => SelectModelObject(selection, updateViewer: false);
        glView.ViewerCommandRequested += OnViewerCommandRequested;
        centerSplit.Panel1.Controls.Add(glView);
        
        mainTabs = new TabControl { Dock = DockStyle.Fill };
        var tabInput = new TabPage("Input Data");
        CreateInputTab(tabInput);
        var tabResults = new TabPage("Analysis Results");
        CreateResultsTab(tabResults);
        mainTabs.TabPages.AddRange(new[] { tabInput, tabResults });
        centerSplit.Panel2.Controls.Add(mainTabs);

        propertyGrid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            HelpVisible = true,
            ToolbarVisible = true
        };
        rightSplit.Panel2.Controls.Add(propertyGrid);
        
        txtStatus = new TextBox
        {
            Name = "txtStatus",
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill
        };
        layout.Controls.Add(txtStatus, 0, 4);
        
        UpdateStatus("Ready. Load or create a truss structure to begin.");
    }

    private ToolStrip CreateRibbon()
    {
        var ribbon = new ToolStrip
        {
            Dock = DockStyle.Fill,
            GripStyle = ToolStripGripStyle.Hidden,
            ImageScalingSize = new System.Drawing.Size(24, 24),
            AutoSize = true
        };

        AddRibbonButton(ribbon, "New", (_, _) => NewProject());
        AddRibbonButton(ribbon, "Open", (_, _) => OpenProject());
        AddRibbonButton(ribbon, "Save", (_, _) => SaveProject());
        ribbon.Items.Add(new ToolStripSeparator());
        AddRibbonButton(ribbon, "Add Node", (_, _) => AddNode());
        AddRibbonButton(ribbon, "Add Frame", (_, _) => AddElement());
        AddRibbonButton(ribbon, "Truss Sample", (_, _) => LoadSampleStructure());
        AddRibbonButton(ribbon, "Frame3D Sample", (_, _) => LoadFrame3DSample());
        ribbon.Items.Add(new ToolStripSeparator());
        AddRibbonButton(ribbon, "Validate", (_, _) => ValidateCurrentModel(showDialog: true));
        AddRibbonButton(ribbon, "Run Analysis", (_, _) => RunAnalysis());
        AddRibbonButton(ribbon, "Show Results", (_, _) => mainTabs!.SelectedIndex = 1);
        AddRibbonButton(ribbon, "Export", (_, _) => ExportReport());
        ribbon.Items.Add(new ToolStripSeparator());
        AddRibbonButton(ribbon, "Iso", (_, _) => glView?.RefreshView());
        AddRibbonButton(ribbon, "Fit", (_, _) => glView?.RefreshView());

        return ribbon;
    }

    private ToolStrip CreateSelectorStrip()
    {
        var strip = new ToolStrip { Dock = DockStyle.Fill, GripStyle = ToolStripGripStyle.Hidden };
        strip.Items.Add(new ToolStripLabel("Coordinate: Right-handed Z-up | XY plan, Z vertical, gravity -Z"));
        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(new ToolStripLabel("Load Case"));
        activeLoadCaseCombo = new ToolStripComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
        activeLoadCaseCombo.SelectedIndexChanged += (_, _) => UpdateActiveLoadSelection();
        strip.Items.Add(activeLoadCaseCombo);
        strip.Items.Add(new ToolStripLabel("Combination"));
        activeCombinationCombo = new ToolStripComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
        activeCombinationCombo.SelectedIndexChanged += (_, _) => UpdateActiveLoadSelection();
        strip.Items.Add(activeCombinationCombo);
        return strip;
    }

    private static void AddRibbonButton(ToolStrip ribbon, string text, EventHandler handler)
    {
        var button = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        button.Click += handler;
        ribbon.Items.Add(button);
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
        dgvNodes.Columns.Add(new DataGridViewCheckBoxColumn { Name = "CRX", HeaderText = "Fix RX" });
        dgvNodes.Columns.Add(new DataGridViewCheckBoxColumn { Name = "CRY", HeaderText = "Fix RY" });
        dgvNodes.Columns.Add(new DataGridViewCheckBoxColumn { Name = "CRZ", HeaderText = "Fix RZ" });
        dgvNodes.Columns.Add(new DataGridViewTextBoxColumn { Name = "FX", HeaderText = "Force X (N)" });
        dgvNodes.Columns.Add(new DataGridViewTextBoxColumn { Name = "FY", HeaderText = "Force Y (N)" });
        dgvNodes.Columns.Add(new DataGridViewTextBoxColumn { Name = "FZ", HeaderText = "Force Z (N)" });
        dgvNodes.Columns.Add(new DataGridViewTextBoxColumn { Name = "MX", HeaderText = "Moment X (N-m)" });
        dgvNodes.Columns.Add(new DataGridViewTextBoxColumn { Name = "MY", HeaderText = "Moment Y (N-m)" });
        dgvNodes.Columns.Add(new DataGridViewTextBoxColumn { Name = "MZ", HeaderText = "Moment Z (N-m)" });
        
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
        dgvElements.Columns.Add(new DataGridViewComboBoxColumn { Name = "Type", HeaderText = "Type", DataSource = Enum.GetNames(typeof(ElementType)) });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "MaterialId", HeaderText = "Mat ID" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "SectionId", HeaderText = "Sec ID" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "StartNode", HeaderText = "Start Node" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndNode", HeaderText = "End Node" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "Area", HeaderText = "Area (m²)" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "Iy", HeaderText = "Iy (m4)" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "Iz", HeaderText = "Iz (m4)" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "J", HeaderText = "J (m4)" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "EModulus", HeaderText = "E (Pa)" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "Density", HeaderText = "Density (kg/m³)" });
        
        dgvElements.Columns.Add(new DataGridViewComboBoxColumn { Name = "MaterialType", HeaderText = "Material", DataSource = Enum.GetNames(typeof(MaterialType)) });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "Fy", HeaderText = "Fy (Pa)" });
        dgvElements.Columns.Add(new DataGridViewTextBoxColumn { Name = "Roll", HeaderText = "Roll (deg)" });
        dgvElements.Columns.Add(new DataGridViewCheckBoxColumn { Name = "RelSY", HeaderText = "Rel i-My" });
        dgvElements.Columns.Add(new DataGridViewCheckBoxColumn { Name = "RelSZ", HeaderText = "Rel i-Mz" });
        dgvElements.Columns.Add(new DataGridViewCheckBoxColumn { Name = "RelEY", HeaderText = "Rel j-My" });
        dgvElements.Columns.Add(new DataGridViewCheckBoxColumn { Name = "RelEZ", HeaderText = "Rel j-Mz" });

        grpElements.Controls.Add(dgvElements);
        splitContainer.Panel2.Controls.Add(grpElements);
        
        var inputTabs = new TabControl { Dock = DockStyle.Fill };
        var geometryPage = new TabPage("Geometry");
        geometryPage.Controls.Add(splitContainer);
        inputTabs.TabPages.Add(geometryPage);
        inputTabs.TabPages.Add(CreateMaterialsPage());
        inputTabs.TabPages.Add(CreateSectionsPage());
        inputTabs.TabPages.Add(CreateLoadsPage());
        inputTabs.TabPages.Add(CreateCombinationsPage());
        inputTabs.TabPages.Add(CreateValidationPage());
        tab.Controls.Add(inputTabs);
        
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

        var btnLoadFrameSample = new Button { Text = "Load Frame3D", Width = 110, Margin = new Padding(5) };
        btnLoadFrameSample.Click += (s, e) => LoadFrame3DSample();
        
        pnlButtons.Controls.AddRange(new Control[] { btnLoadFrameSample, btnLoadSample, btnAddElement, btnAddNode });
        tab.Controls.Add(pnlButtons);
    }

    private TabPage CreateMaterialsPage()
    {
        var page = new TabPage("Materials");
        dgvMaterials = CreateEditableGrid();
        dgvMaterials.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID" });
        dgvMaterials.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name" });
        dgvMaterials.Columns.Add(new DataGridViewComboBoxColumn { Name = "Type", HeaderText = "Type", DataSource = Enum.GetNames(typeof(MaterialType)) });
        dgvMaterials.Columns.Add(new DataGridViewTextBoxColumn { Name = "E", HeaderText = "E (Pa)" });
        dgvMaterials.Columns.Add(new DataGridViewTextBoxColumn { Name = "G", HeaderText = "G (Pa)" });
        dgvMaterials.Columns.Add(new DataGridViewTextBoxColumn { Name = "Nu", HeaderText = "Nu" });
        dgvMaterials.Columns.Add(new DataGridViewTextBoxColumn { Name = "Density", HeaderText = "Density (kg/m3)" });
        dgvMaterials.Columns.Add(new DataGridViewTextBoxColumn { Name = "Fy", HeaderText = "Fy (Pa)" });
        dgvMaterials.Columns.Add(new DataGridViewTextBoxColumn { Name = "Fu", HeaderText = "Fu (Pa)" });
        dgvMaterials.Columns.Add(new DataGridViewTextBoxColumn { Name = "Fc", HeaderText = "f'c (Pa)" });
        page.Controls.Add(dgvMaterials);
        return page;
    }

    private TabPage CreateSectionsPage()
    {
        var page = new TabPage("Sections");
        dgvSections = CreateEditableGrid();
        dgvSections.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID" });
        dgvSections.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name" });
        dgvSections.Columns.Add(new DataGridViewComboBoxColumn { Name = "Type", HeaderText = "Type", DataSource = Enum.GetNames(typeof(SectionType)) });
        dgvSections.Columns.Add(new DataGridViewTextBoxColumn { Name = "A", HeaderText = "A (m2)" });
        dgvSections.Columns.Add(new DataGridViewTextBoxColumn { Name = "Iy", HeaderText = "Iy (m4)" });
        dgvSections.Columns.Add(new DataGridViewTextBoxColumn { Name = "Iz", HeaderText = "Iz (m4)" });
        dgvSections.Columns.Add(new DataGridViewTextBoxColumn { Name = "J", HeaderText = "J (m4)" });
        dgvSections.Columns.Add(new DataGridViewTextBoxColumn { Name = "Width", HeaderText = "Width (m)" });
        dgvSections.Columns.Add(new DataGridViewTextBoxColumn { Name = "Depth", HeaderText = "Depth (m)" });
        dgvSections.Columns.Add(new DataGridViewTextBoxColumn { Name = "RebarArea", HeaderText = "As (m2)" });
        dgvSections.Columns.Add(new DataGridViewTextBoxColumn { Name = "EffectiveDepth", HeaderText = "d (m)" });
        page.Controls.Add(dgvSections);
        return page;
    }

    private TabPage CreateLoadsPage()
    {
        var page = new TabPage("Loads");
        dgvLoads = CreateEditableGrid();
        dgvLoads.Columns.Add(new DataGridViewComboBoxColumn { Name = "Kind", HeaderText = "Kind", DataSource = new[] { "Nodal", "MemberPoint", "MemberDistributed" } });
        dgvLoads.Columns.Add(new DataGridViewTextBoxColumn { Name = "CaseId", HeaderText = "Case" });
        dgvLoads.Columns.Add(new DataGridViewTextBoxColumn { Name = "NodeId", HeaderText = "Node" });
        dgvLoads.Columns.Add(new DataGridViewTextBoxColumn { Name = "ElementId", HeaderText = "Element" });
        dgvLoads.Columns.Add(new DataGridViewTextBoxColumn { Name = "RelativeDistance", HeaderText = "a/L" });
        dgvLoads.Columns.Add(new DataGridViewTextBoxColumn { Name = "StartRelativeDistance", HeaderText = "Start a/L" });
        dgvLoads.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndRelativeDistance", HeaderText = "End a/L" });
        dgvLoads.Columns.Add(new DataGridViewComboBoxColumn { Name = "Direction", HeaderText = "Direction", DataSource = Enum.GetNames(typeof(LoadDirection)) });
        dgvLoads.Columns.Add(new DataGridViewTextBoxColumn { Name = "FX", HeaderText = "Fx or wx" });
        dgvLoads.Columns.Add(new DataGridViewTextBoxColumn { Name = "FY", HeaderText = "Fy or wy" });
        dgvLoads.Columns.Add(new DataGridViewTextBoxColumn { Name = "FZ", HeaderText = "Fz or wz" });
        dgvLoads.Columns.Add(new DataGridViewTextBoxColumn { Name = "MX", HeaderText = "Mx" });
        dgvLoads.Columns.Add(new DataGridViewTextBoxColumn { Name = "MY", HeaderText = "My" });
        dgvLoads.Columns.Add(new DataGridViewTextBoxColumn { Name = "MZ", HeaderText = "Mz" });
        page.Controls.Add(dgvLoads);
        return page;
    }

    private TabPage CreateCombinationsPage()
    {
        var page = new TabPage("Combinations");
        dgvCombinations = CreateEditableGrid();
        dgvCombinations.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID" });
        dgvCombinations.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name" });
        dgvCombinations.Columns.Add(new DataGridViewTextBoxColumn { Name = "Factors", HeaderText = "Factors e.g. DL=1.2;LL=1.6" });
        page.Controls.Add(dgvCombinations);
        return page;
    }

    private TabPage CreateValidationPage()
    {
        var page = new TabPage("Validation");
        dgvValidation = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false
        };
        dgvValidation.Columns.Add(new DataGridViewTextBoxColumn { Name = "Severity", HeaderText = "Severity" });
        dgvValidation.Columns.Add(new DataGridViewTextBoxColumn { Name = "Message", HeaderText = "Message" });
        dgvValidation.Columns.Add(new DataGridViewTextBoxColumn { Name = "ObjectType", HeaderText = "Object Type", Visible = false });
        dgvValidation.Columns.Add(new DataGridViewTextBoxColumn { Name = "ObjectId", HeaderText = "Object ID", Visible = false });
        dgvValidation.CellDoubleClick += (_, e) => FocusValidationMessage(e.RowIndex);
        page.Controls.Add(dgvValidation);
        return page;
    }

    private static DataGridView CreateEditableGrid() => new()
    {
        Dock = DockStyle.Fill,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        AllowUserToAddRows = true,
        AllowUserToDeleteRows = true
    };
    
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
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "RotX", HeaderText = "Rot X (rad)" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "RotY", HeaderText = "Rot Y (rad)" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "RotZ", HeaderText = "Rot Z (rad)" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rx", HeaderText = "Reaction X (N)" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ry", HeaderText = "Reaction Y (N)" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rz", HeaderText = "Reaction Z (N)" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "RMx", HeaderText = "Reaction MX (N-m)" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "RMy", HeaderText = "Reaction MY (N-m)" });
        dgvResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "RMz", HeaderText = "Reaction MZ (N-m)" });
        
        grpDisplacements.Controls.Add(dgvResults);
        splitContainer.Panel1.Controls.Add(grpDisplacements);
        
        var resultTabs = new TabControl { Dock = DockStyle.Fill };

        // Element forces
        var forcesPage = new TabPage("Element Forces");
        dgvForces = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = true
        };
        
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "ElemId", HeaderText = "Element ID" });
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "Force", HeaderText = "Axial Force (N)" });
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "ShearY", HeaderText = "Shear Y (N)" });
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "ShearZ", HeaderText = "Shear Z (N)" });
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "Torsion", HeaderText = "Torsion (N-m)" });
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "MomentY", HeaderText = "Moment Y (N-m)" });
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "MomentZ", HeaderText = "Moment Z (N-m)" });
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "Stress", HeaderText = "Stress (MPa)" });
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "Utilization", HeaderText = "Util." });
        dgvForces.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status" });

        forcesPage.Controls.Add(dgvForces);
        resultTabs.TabPages.Add(forcesPage);

        var stationsPage = new TabPage("Station Values");
        dgvStations = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = true
        };
        dgvStations.Columns.Add(new DataGridViewTextBoxColumn { Name = "ElementId", HeaderText = "Element" });
        dgvStations.Columns.Add(new DataGridViewTextBoxColumn { Name = "Station", HeaderText = "Station a/L" });
        dgvStations.Columns.Add(new DataGridViewTextBoxColumn { Name = "Axial", HeaderText = "Axial (N)" });
        dgvStations.Columns.Add(new DataGridViewTextBoxColumn { Name = "ShearY", HeaderText = "Shear Y (N)" });
        dgvStations.Columns.Add(new DataGridViewTextBoxColumn { Name = "ShearZ", HeaderText = "Shear Z (N)" });
        dgvStations.Columns.Add(new DataGridViewTextBoxColumn { Name = "Torsion", HeaderText = "Torsion (N-m)" });
        dgvStations.Columns.Add(new DataGridViewTextBoxColumn { Name = "MomentY", HeaderText = "Moment Y (N-m)" });
        dgvStations.Columns.Add(new DataGridViewTextBoxColumn { Name = "MomentZ", HeaderText = "Moment Z (N-m)" });
        stationsPage.Controls.Add(dgvStations);
        resultTabs.TabPages.Add(stationsPage);

        var checksPage = new TabPage("Design Checks");
        dgvDesignChecks = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = true
        };
        dgvDesignChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "ElementId", HeaderText = "Element" });
        dgvDesignChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "CheckType", HeaderText = "Check" });
        dgvDesignChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Demand", HeaderText = "Demand" });
        dgvDesignChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Capacity", HeaderText = "Capacity" });
        dgvDesignChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Utilization", HeaderText = "Util." });
        dgvDesignChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status" });
        dgvDesignChecks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "Notes" });
        checksPage.Controls.Add(dgvDesignChecks);
        resultTabs.TabPages.Add(checksPage);

        splitContainer.Panel2.Controls.Add(resultTabs);
        
        tab.Controls.Add(splitContainer);
    }
    
    private void Create3DViewTab(TabPage tab)
    {
        glView = new HelixStructuralView { Dock = DockStyle.Fill };
        tab.Controls.Add(glView);
    }

    private void PopulateObjectTree()
    {
        if (objectTree == null)
            return;

        objectTree.AfterCheck -= ObjectTreeAfterCheck;
        objectTree.Nodes.Clear();
        var display = objectTree.Nodes.Add("Display Layers");
        foreach (var layer in new[] { "Grid", "Nodes", "Elements", "Supports", "Loads", "Load Labels", "Reaction Labels", "Labels", "Local Axes", "Deformed Shape", "Diagrams" })
        {
            display.Nodes.Add(new TreeNode(layer) { Checked = true, Tag = $"Layer:{layer}" });
        }

        var model = _structuralModel ?? StructuralModel.FromTrussSolver(_solver);
        var nodesRoot = objectTree.Nodes.Add("Nodes");
        foreach (var node in model.Nodes)
            nodesRoot.Nodes.Add(new TreeNode($"N{node.Id}  X={node.Position.X:g}, Y={node.Position.Y:g}, Z={node.Position.Z:g}") { Tag = new SelectedModelObject { Type = SelectedModelObjectType.Node, Id = node.Id, Name = $"Node {node.Id}" } });

        var elementsRoot = objectTree.Nodes.Add("Elements");
        foreach (var element in model.Elements)
            elementsRoot.Nodes.Add(new TreeNode($"E{element.Id}  {element.Type}  {element.StartNodeId}->{element.EndNodeId}") { Tag = new SelectedModelObject { Type = SelectedModelObjectType.Element, Id = element.Id, Name = $"Element {element.Id}" } });

        var materialsRoot = objectTree.Nodes.Add("Materials");
        foreach (var material in model.Materials)
            materialsRoot.Nodes.Add(new TreeNode($"{material.Id}: {material.Name} ({material.Type})") { Tag = new SelectedModelObject { Type = SelectedModelObjectType.Material, Id = material.Id, Name = material.Name } });

        var sectionsRoot = objectTree.Nodes.Add("Sections");
        foreach (var section in model.Sections)
            sectionsRoot.Nodes.Add(new TreeNode($"{section.Id}: {section.Name} ({section.Type})") { Tag = new SelectedModelObject { Type = SelectedModelObjectType.Section, Id = section.Id, Name = section.Name } });

        var loadsRoot = objectTree.Nodes.Add("Load Cases");
        foreach (var loadCase in model.LoadCases)
            loadsRoot.Nodes.Add(new TreeNode($"{loadCase.CaseId}: {loadCase.Name}") { Tag = new SelectedModelObject { Type = SelectedModelObjectType.LoadCase, Id = 0, Name = loadCase.CaseId } });

        var combinationsRoot = objectTree.Nodes.Add("Combinations");
        foreach (var combination in model.LoadCombinations)
            combinationsRoot.Nodes.Add(new TreeNode($"{combination.CombinationId}: {combination.Name}") { Tag = new SelectedModelObject { Type = SelectedModelObjectType.LoadCombination, Id = 0, Name = combination.CombinationId } });

        if (_structuralResult != null)
        {
            var resultsRoot = objectTree.Nodes.Add("Results");
            resultsRoot.Nodes.Add($"Case: {_structuralResult.LoadCaseName}");
            resultsRoot.Nodes.Add($"Max displacement: {_structuralResult.MaxDisplacement * 1000:F3} mm");
            resultsRoot.Nodes.Add($"Max utilization: {_structuralResult.MaxUtilization:F3}");
        }

        objectTree.ExpandAll();
        objectTree.AfterCheck += ObjectTreeAfterCheck;
        RefreshLoadSelectors(model);
    }

    private void ObjectTreeAfterCheck(object? sender, TreeViewEventArgs e) => OnObjectTreeChecked(e.Node);

    private void OnObjectTreeChecked(TreeNode? node)
    {
        if (node?.Tag is string tag && tag.StartsWith("Layer:", StringComparison.Ordinal))
            glView?.SetLayer(tag["Layer:".Length..], node.Checked);
    }

    private void OnObjectTreeSelected(TreeNode? node)
    {
        if (node?.Tag is not SelectedModelObject selection)
            return;

        SelectModelObject(selection, updateViewer: true);
    }

    private void SelectModelObject(SelectedModelObject selection, bool updateViewer)
    {
        if (_syncingSelection)
            return;

        _syncingSelection = true;
        try
        {
            if (updateViewer)
                glView?.SelectObject(selection);
            propertyGrid!.SelectedObject = ResolveSelectedObject(selection);
            SelectTreeNode(selection);
            SelectGridRow(selection);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void SelectTreeNode(SelectedModelObject selection)
    {
        if (objectTree == null)
            return;

        var node = FindTreeNode(objectTree.Nodes, selection);
        if (node != null)
        {
            objectTree.SelectedNode = node;
            node.EnsureVisible();
        }
    }

    private static TreeNode? FindTreeNode(TreeNodeCollection nodes, SelectedModelObject selection)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is SelectedModelObject candidate &&
                candidate.Type == selection.Type &&
                candidate.Id == selection.Id &&
                (selection.Type is not (SelectedModelObjectType.LoadCase or SelectedModelObjectType.LoadCombination) ||
                    string.Equals(candidate.Name, selection.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return node;
            }

            var child = FindTreeNode(node.Nodes, selection);
            if (child != null)
                return child;
        }

        return null;
    }

    private void SelectGridRow(SelectedModelObject selection)
    {
        DataGridView? grid = selection.Type switch
        {
            SelectedModelObjectType.Node => dgvNodes,
            SelectedModelObjectType.Element => dgvElements,
            SelectedModelObjectType.Material => dgvMaterials,
            SelectedModelObjectType.Section => dgvSections,
            _ => null
        };
        if (grid == null)
            return;

        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow)
                continue;
            if (TryReadInt(row, "Id", out int id) && id == selection.Id)
            {
                grid.ClearSelection();
                row.Selected = true;
                grid.CurrentCell = row.Cells[0];
                mainTabs!.SelectedIndex = 0;
                return;
            }
        }
    }

    private void OnViewerCommandRequested(object? sender, ViewerCommandRequestedEventArgs e)
    {
        switch (e.Command)
        {
            case "AddNode":
                AddNode();
                break;
            case "AddFrameMember":
                AddElementOfType(ElementType.Frame3D);
                break;
            case "AddTrussMember":
                AddElementOfType(ElementType.Truss);
                break;
            case "AddNodalLoad":
                AddNodalLoadFromSelection(e.Selection);
                break;
            case "AddMemberDistributedLoad":
                AddMemberDistributedLoadFromSelection(e.Selection);
                break;
            case "Duplicate":
                DuplicateSelection(e.Selection);
                break;
            case "Delete":
                DeleteSelection(e.Selection);
                break;
            case "ShowProperties":
                SelectModelObject(e.Selection, updateViewer: true);
                break;
        }
    }

    private object? ResolveSelectedObject(SelectedModelObject selection)
    {
        var model = _structuralModel ?? StructuralModel.FromTrussSolver(_solver);
        return selection.Type switch
        {
            SelectedModelObjectType.Node => model.Nodes.FirstOrDefault(n => n.Id == selection.Id),
            SelectedModelObjectType.Element => model.Elements.FirstOrDefault(e => e.Id == selection.Id),
            SelectedModelObjectType.Material => model.Materials.FirstOrDefault(m => m.Id == selection.Id),
            SelectedModelObjectType.Section => model.Sections.FirstOrDefault(s => s.Id == selection.Id),
            _ => selection
        };
    }

    private void RefreshLoadSelectors(StructuralModel model)
    {
        if (activeLoadCaseCombo == null || activeCombinationCombo == null)
            return;

        activeLoadCaseCombo.Items.Clear();
        activeLoadCaseCombo.Items.Add("Default");
        foreach (var loadCase in model.LoadCases)
            activeLoadCaseCombo.Items.Add(loadCase.CaseId);
        activeLoadCaseCombo.SelectedItem = string.IsNullOrWhiteSpace(model.ActiveLoadCaseId) ? "Default" : model.ActiveLoadCaseId;
        if (activeLoadCaseCombo.SelectedIndex < 0)
            activeLoadCaseCombo.SelectedIndex = 0;

        activeCombinationCombo.Items.Clear();
        activeCombinationCombo.Items.Add("None");
        foreach (var combination in model.LoadCombinations)
            activeCombinationCombo.Items.Add(combination.CombinationId);
        activeCombinationCombo.SelectedIndex = 0;
    }

    private void UpdateActiveLoadSelection()
    {
        if (_structuralModel == null || activeLoadCaseCombo == null)
            return;

        string selected = Convert.ToString(activeLoadCaseCombo.SelectedItem, CultureInfo.InvariantCulture) ?? "Default";
        _structuralModel.ActiveLoadCaseId = selected == "Default" ? string.Empty : selected;
    }
    
    private void LoadSampleStructure()
    {
        _solver = new TrussSolver();
        _structuralModel = null;
        _structuralResult = null;
        
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
        glView?.SetModel(_solver, _solver.LastResult);
        PopulateObjectTree();
        UpdateStatus("Sample tripod structure loaded. Click 'Run Analysis' to calculate results.");
    }

    private void LoadFrame3DSample()
    {
        _solver = new TrussSolver();
        _structuralResult = null;

        var model = new StructuralModel();
        model.Nodes.AddRange(new[]
        {
            new Node(1, new Point3D(0, 0, 0))
            {
                ConstraintX = true, ConstraintY = true, ConstraintZ = true,
                ConstraintRX = true, ConstraintRY = true, ConstraintRZ = true
            },
            new Node(2, new Point3D(6, 0, 0))
            {
                ConstraintX = true, ConstraintY = true, ConstraintZ = true,
                ConstraintRX = true, ConstraintRY = true, ConstraintRZ = true
            },
            new Node(3, new Point3D(0, 0, 4)),
            new Node(4, new Point3D(6, 0, 4))
        });

        model.Materials.Add(new Material
        {
            Id = 1,
            Name = "Frame steel",
            Type = MaterialType.Steel,
            YoungsModulus = 200e9,
            ShearModulus = 76.9e9,
            PoissonsRatio = 0.3,
            Density = 7850,
            YieldStrength = 250e6,
            UltimateStrength = 400e6
        });

        model.Sections.Add(new Section
        {
            Id = 1,
            Name = "Generic frame member",
            Type = SectionType.Generic,
            Area = 0.006,
            Iy = 8.0e-5,
            Iz = 4.0e-5,
            J = 1.2e-5,
            Width = 0.20,
            Depth = 0.30
        });

        model.Elements.AddRange(new StructuralElement[]
        {
            new FrameElement3D(1, 1, 3, 1, 1),
            new FrameElement3D(2, 2, 4, 1, 1),
            new FrameElement3D(3, 3, 4, 1, 1)
        });

        model.Loads.Add(new NodalLoad
        {
            LoadCaseId = "WIND",
            NodeId = 3,
            Force = new Vector3D(12000, 0, 0)
        });
        model.Loads.Add(new MemberDistributedLoad
        {
            LoadCaseId = "LIVE",
            ElementId = 3,
            Direction = LoadDirection.GlobalZ,
            ForcePerLength = new Vector3D(0, 0, -8000)
        });

        model.EnsureDefaultLoadTemplates();
        _structuralModel = model;
        PopulateGrids(model);
        glView?.SetModel(model, null);
        PopulateObjectTree();
        UpdateStatus("Sample Frame3D portal loaded. Click 'Run Analysis' to calculate shear, moment, torsion and utilization.");
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
                node.ConstraintRX, node.ConstraintRY, node.ConstraintRZ,
                node.AppliedForce.X, node.AppliedForce.Y, node.AppliedForce.Z,
                node.AppliedMoment.X, node.AppliedMoment.Y, node.AppliedMoment.Z
            );
        }
        
        dgvElements.Rows.Clear();
        foreach (var elem in _solver.GetElements())
        {
            dgvElements.Rows.Add(
                elem.Id, ElementType.Truss.ToString(), elem.Id, elem.Id, elem.StartNodeId, elem.EndNodeId,
                elem.Area, elem.Area * 1e-4, elem.Area * 1e-4, elem.Area * 1e-4,
                elem.Material.YoungsModulus, elem.Material.Density,
                elem.Material.Type.ToString(), elem.Material.YieldStrength,
                0, false, false, false, false
            );
        }

        PopulateLibraryGridsFromSolver();
        PopulateObjectTree();
    }

    private void PopulateLibraryGridsFromSolver()
    {
        dgvMaterials?.Rows.Clear();
        dgvSections?.Rows.Clear();
        dgvLoads?.Rows.Clear();
        dgvCombinations?.Rows.Clear();
        dgvValidation?.Rows.Clear();

        foreach (var elem in _solver.GetElements())
        {
            if (dgvMaterials != null)
            {
                dgvMaterials.Rows.Add(
                    elem.Id,
                    elem.Material.Name,
                    elem.Material.Type.ToString(),
                    elem.Material.YoungsModulus,
                    elem.Material.ShearModulus,
                    elem.Material.PoissonsRatio,
                    elem.Material.Density,
                    elem.Material.YieldStrength,
                    elem.Material.UltimateStrength,
                    elem.Material.ConcreteCompressiveStrength);
            }

            if (dgvSections != null)
            {
                dgvSections.Rows.Add(
                    elem.Id,
                    $"Section {elem.Id}",
                    SectionType.Generic.ToString(),
                    elem.Area,
                    elem.Area * 1e-4,
                    elem.Area * 1e-4,
                    elem.Area * 1e-4,
                    Math.Sqrt(elem.Area),
                    Math.Sqrt(elem.Area),
                    0,
                    0);
            }
        }

        // Node-grid loads are already analyzed by default; keep the explicit load grid empty for sample models.
    }
    
    private void RunAnalysis()
    {
        try
        {
            _structuralModel = BuildStructuralModelFromGrids();
            var structuralSolver = new StructuralSolver(_structuralModel);
            var messages = structuralSolver.ValidateModel();
            var errors = messages.Where(m => m.Severity == "Error").ToList();
            if (errors.Count > 0)
            {
                string validation = string.Join(Environment.NewLine, errors.Select(e => e.Message));
                MessageBox.Show(validation, "Model Validation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus(validation);
                return;
            }

            UpdateStatus("Running structural analysis...");
            string selectedCombination = Convert.ToString(activeCombinationCombo?.SelectedItem, CultureInfo.InvariantCulture) ?? "None";
            string selectedLoadCase = Convert.ToString(activeLoadCaseCombo?.SelectedItem, CultureInfo.InvariantCulture) ?? "Default";
            var result = selectedCombination != "None" && _structuralModel.LoadCombinations.Any(c => c.CombinationId == selectedCombination)
                ? structuralSolver.AnalyzeCombination(selectedCombination)
                : selectedLoadCase != "Default" && _structuralModel.LoadCases.Any(lc => lc.CaseId == selectedLoadCase)
                    ? structuralSolver.Analyze(selectedLoadCase)
                    : structuralSolver.Analyze();
            _structuralResult = result;
            
            if (result.Equilibrium.IsSatisfied)
            {
                UpdateStatus($"Analysis completed. Equilibrium satisfied. Max utilization = {result.MaxUtilization:F3}.");
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

        if (dgvForces != null)
        {
            dgvForces.Rows.Clear();
            foreach (var elem in result.Elements)
            {
                var check = result.SafetyChecks.ElementChecks.FirstOrDefault(c => c.ElementId == elem.Id);
                dgvForces.Rows.Add(
                    elem.Id,
                    elem.AxialForce.ToString("F2"),
                    (elem.Stress / 1e6).ToString("F3"),
                    elem.Strain.ToString("E4"),
                    elem.Length.ToString("F3"),
                    (check?.UtilizationRatio ?? 0).ToString("F3"),
                    check?.Status ?? "N/A"
                );
            }
        }
        dgvStations?.Rows.Clear();
        dgvDesignChecks?.Rows.Clear();
        glView?.SetModel(_solver, result);
        
        mainTabs!.SelectedIndex = 1;
    }
    
    private void CheckEquilibrium()
    {
        if (_structuralResult != null)
        {
            string structuralMsg = _structuralResult.Equilibrium.IsSatisfied
                ? "Structure is in equilibrium."
                : "Equilibrium NOT satisfied.";
            MessageBox.Show(structuralMsg, "Equilibrium Check", MessageBoxButtons.OK,
                _structuralResult.Equilibrium.IsSatisfied ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            return;
        }

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
    
    private void NewProject()
    {
        _solver = new TrussSolver();
        _structuralModel = null;
        _structuralResult = null;
        dgvNodes?.Rows.Clear();
        dgvElements?.Rows.Clear();
        dgvResults?.Rows.Clear();
        dgvForces?.Rows.Clear();
        dgvStations?.Rows.Clear();
        dgvDesignChecks?.Rows.Clear();
        glView?.SetModel(_solver, _solver.LastResult);
        PopulateObjectTree();
        UpdateStatus("New empty project created.");
    }
    private void OpenProject()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "JSON Files|*.json",
            Title = "Open Truss Model"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            string json = File.ReadAllText(dlg.FileName);
            _structuralModel = StructureImporterExporter.ImportStructuralModelFromJson(json);
            _solver = new TrussSolver();
            PopulateGrids(_structuralModel);
            PopulateObjectTree();
            UpdateStatus($"Loaded model from {dlg.FileName}");
        }
    }

    private void PopulateGrids(StructuralModel model)
    {
        if (dgvNodes == null || dgvElements == null) return;

        dgvNodes.Rows.Clear();
        foreach (var node in model.Nodes)
        {
            dgvNodes.Rows.Add(
                node.Id, node.Position.X, node.Position.Y, node.Position.Z,
                node.ConstraintX, node.ConstraintY, node.ConstraintZ,
                node.ConstraintRX, node.ConstraintRY, node.ConstraintRZ,
                node.AppliedForce.X, node.AppliedForce.Y, node.AppliedForce.Z,
                node.AppliedMoment.X, node.AppliedMoment.Y, node.AppliedMoment.Z
            );
        }

        dgvElements.Rows.Clear();
        foreach (var element in model.Elements)
        {
            var material = model.Materials.FirstOrDefault(m => m.Id == element.MaterialId) ?? Material.StructuralSteel;
            var section = model.Sections.FirstOrDefault(s => s.Id == element.SectionId) ?? Section.Generic(0, "Default", 0.001, 1e-6, 1e-6, 1e-6);
            dgvElements.Rows.Add(
                element.Id, element.Type.ToString(), element.MaterialId, element.SectionId, element.StartNodeId, element.EndNodeId,
                section.Area, section.Iy, section.Iz, section.J,
                material.YoungsModulus, material.Density,
                material.Type.ToString(), material.YieldStrength,
                element.RollAngleRadians * 180.0 / Math.PI,
                element.Releases.StartMomentY,
                element.Releases.StartMomentZ,
                element.Releases.EndMomentY,
                element.Releases.EndMomentZ
            );
        }

        dgvMaterials?.Rows.Clear();
        foreach (var material in model.Materials)
        {
            dgvMaterials?.Rows.Add(material.Id, material.Name, material.Type.ToString(), material.YoungsModulus,
                material.ShearModulus, material.PoissonsRatio, material.Density, material.YieldStrength,
                material.UltimateStrength, material.ConcreteCompressiveStrength);
        }

        dgvSections?.Rows.Clear();
        foreach (var section in model.Sections)
        {
            dgvSections?.Rows.Add(section.Id, section.Name, section.Type.ToString(), section.Area, section.Iy,
                section.Iz, section.J, section.Width, section.Depth, section.RebarArea, section.EffectiveDepth);
        }

        dgvLoads?.Rows.Clear();
        foreach (var load in model.Loads)
        {
            switch (load)
            {
                case NodalLoad nodal:
                    dgvLoads?.Rows.Add("Nodal", nodal.LoadCaseId, nodal.NodeId, "", 0.5, 0, 1, LoadDirection.GlobalZ.ToString(),
                        nodal.Force.X, nodal.Force.Y, nodal.Force.Z, nodal.Moment.X, nodal.Moment.Y, nodal.Moment.Z);
                    break;
                case MemberPointLoad point:
                    dgvLoads?.Rows.Add("MemberPoint", point.LoadCaseId, "", point.ElementId, point.RelativeDistance, 0, 1, point.Direction.ToString(),
                        point.Force.X, point.Force.Y, point.Force.Z, point.Moment.X, point.Moment.Y, point.Moment.Z);
                    break;
                case MemberDistributedLoad distributed:
                    dgvLoads?.Rows.Add("MemberDistributed", distributed.LoadCaseId, "", distributed.ElementId, 0.5, distributed.StartRelativeDistance, distributed.EndRelativeDistance, distributed.Direction.ToString(),
                        distributed.ForcePerLength.X, distributed.ForcePerLength.Y, distributed.ForcePerLength.Z, 0, 0, 0);
                    break;
            }
        }

        dgvCombinations?.Rows.Clear();
        foreach (var combination in model.LoadCombinations)
        {
            string factors = string.Join(";", combination.LoadCases.Select(kvp => $"{kvp.Key}={kvp.Value.ToString(CultureInfo.InvariantCulture)}"));
            dgvCombinations?.Rows.Add(combination.CombinationId, combination.Name, factors);
        }

        glView?.SetModel(model, _structuralResult);
        PopulateObjectTree();
    }

    private void DisplayResults(StructuralAnalysisResult result)
    {
        if (dgvResults == null) return;

        dgvResults.Rows.Clear();
        foreach (var node in result.NodeResults)
        {
            dgvResults.Rows.Add(
                node.NodeId,
                (node.Displacement.X * 1000).ToString("F4"),
                (node.Displacement.Y * 1000).ToString("F4"),
                (node.Displacement.Z * 1000).ToString("F4"),
                node.Rotation.X.ToString("E4"),
                node.Rotation.Y.ToString("E4"),
                node.Rotation.Z.ToString("E4"),
                node.ReactionForce.X.ToString("F2"),
                node.ReactionForce.Y.ToString("F2"),
                node.ReactionForce.Z.ToString("F2"),
                node.ReactionMoment.X.ToString("F2"),
                node.ReactionMoment.Y.ToString("F2"),
                node.ReactionMoment.Z.ToString("F2")
            );
        }

        if (dgvForces != null)
        {
            dgvForces.Rows.Clear();
            foreach (var elem in result.ElementResults)
            {
                var maxCheck = result.DesignChecks
                    .Where(c => c.ElementId == elem.ElementId)
                    .OrderByDescending(c => c.Utilization)
                    .FirstOrDefault();
                dgvForces.Rows.Add(
                    elem.ElementId,
                    elem.AxialForce.ToString("F2"),
                    elem.ShearY.ToString("F2"),
                    elem.ShearZ.ToString("F2"),
                    elem.Torsion.ToString("F2"),
                    elem.MomentY.ToString("F2"),
                    elem.MomentZ.ToString("F2"),
                    (elem.Stress / 1e6).ToString("F3"),
                    (maxCheck?.Utilization ?? 0).ToString("F3"),
                    maxCheck?.Status.ToString() ?? "N/A"
                );
            }
        }

        if (dgvStations != null)
        {
            dgvStations.Rows.Clear();
            foreach (var station in result.ElementResults.SelectMany(e => e.StationResults))
            {
                dgvStations.Rows.Add(
                    station.ElementId,
                    station.RelativePosition.ToString("F2"),
                    station.AxialForce.ToString("F2"),
                    station.ShearY.ToString("F2"),
                    station.ShearZ.ToString("F2"),
                    station.Torsion.ToString("F2"),
                    station.MomentY.ToString("F2"),
                    station.MomentZ.ToString("F2"));
            }
        }

        if (dgvDesignChecks != null)
        {
            dgvDesignChecks.Rows.Clear();
            foreach (var check in result.DesignChecks)
            {
                int rowIndex = dgvDesignChecks.Rows.Add(
                    check.ElementId,
                    check.CheckType,
                    check.Demand.ToString("E4"),
                    check.Capacity.ToString("E4"),
                    check.Utilization.ToString("F3"),
                    check.Status.ToString(),
                    check.Notes);
                dgvDesignChecks.Rows[rowIndex].DefaultCellStyle.BackColor = check.Status == DesignCheckStatus.NG
                    ? Color.MistyRose
                    : check.Status == DesignCheckStatus.MissingData ? Color.LemonChiffon : Color.White;
            }
        }

        if (_structuralModel != null)
            glView?.SetModel(_structuralModel, result);

        PopulateObjectTree();
        mainTabs!.SelectedIndex = 1;
    }

    private void Show3DView()
    {
        if (_structuralModel != null)
            glView?.SetModel(_structuralModel, _structuralResult);
        else
            glView?.SetModel(_solver, _solver.LastResult);

        glView?.RefreshView();
    }

    private void SaveProject()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "JSON Files|*.json",
            Title = "Save Truss Model",
            FileName = "truss-model.json"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _structuralModel = BuildStructuralModelFromGrids();
            File.WriteAllText(dlg.FileName, StructureImporterExporter.ExportStructuralModelToJson(_structuralModel));
            UpdateStatus($"Saved model to {dlg.FileName}");
        }
    }
    
    private void ExportReport()
    {
        if (_structuralResult == null && _solver.LastResult == null)
        {
            MessageBox.Show("Please run analysis first.", "Info");
            return;
        }
        
        var dlg = new SaveFileDialog
        {
            Filter = "Text File|*.txt|CSV File|*.csv|PDF File|*.pdf",
            Title = "Export Analysis Report"
        };
        
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            if (_structuralResult != null)
            {
                ExportStructuralResult(dlg.FileName, _structuralResult, _structuralModel);
            }
            else if (dlg.FilterIndex == 2 && _solver.LastResult != null)
            {
                StructureImporterExporter.ExportResultsToCsv(_solver.LastResult, dlg.FileName);
            }
            else if (dlg.FilterIndex == 3 && _solver.LastResult != null)
            {
                var pdf = new Core.Reporting.PdfReportGenerator(_solver.LastResult);
                pdf.SaveToFile(dlg.FileName);
            }
            else
            {
                ExportToTextFile(dlg.FileName);
            }
            MessageBox.Show($"Report exported to {dlg.FileName}", "Success");
        }
    }

    private static void ExportStructuralResult(string filePath, StructuralAnalysisResult result, StructuralModel? model)
    {
        using var writer = new StreamWriter(filePath);
        writer.WriteLine("3D Structural Analyzer MVP Report");
        writer.WriteLine($"Load case: {result.LoadCaseName}");
        writer.WriteLine($"Equilibrium: {result.Equilibrium.IsSatisfied}");
        writer.WriteLine($"Max displacement (m): {result.MaxDisplacement:E4}");
        writer.WriteLine($"Max utilization: {result.MaxUtilization:F3}");
        writer.WriteLine($"Total DOF: {result.Diagnostics.TotalDof}");
        writer.WriteLine($"Constrained DOF: {result.Diagnostics.ConstrainedDof}");
        writer.WriteLine($"Solver: {result.Diagnostics.SolverName}");
        writer.WriteLine($"Matrix density: {result.Diagnostics.MatrixDensity:F4}");
        writer.WriteLine($"Applied load magnitude: {result.Diagnostics.AppliedLoadMagnitude:E4}");
        writer.WriteLine($"Reaction magnitude: {result.Diagnostics.ReactionMagnitude:E4}");
        writer.WriteLine($"Equilibrium residual magnitude: {result.Diagnostics.EquilibriumResidualMagnitude:E4}");
        writer.WriteLine(result.Diagnostics.Notes);
        writer.WriteLine("Design checks are preliminary MVP checks, not final code-compliant design.");
        writer.WriteLine();

        if (model != null)
        {
            writer.WriteLine("Materials");
            writer.WriteLine("Id,Name,Type,E_Pa,Density,Fy_Pa,Fc_Pa");
            foreach (var m in model.Materials)
                writer.WriteLine($"{m.Id},{m.Name},{m.Type},{m.YoungsModulus:E4},{m.Density:F2},{m.YieldStrength:E4},{m.ConcreteCompressiveStrength:E4}");
            writer.WriteLine();

            writer.WriteLine("Sections");
            writer.WriteLine("Id,Name,Type,A_m2,Iy_m4,Iz_m4,J_m4,Width_m,Depth_m");
            foreach (var s in model.Sections)
                writer.WriteLine($"{s.Id},{s.Name},{s.Type},{s.Area:E4},{s.Iy:E4},{s.Iz:E4},{s.J:E4},{s.Width:F4},{s.Depth:F4}");
            writer.WriteLine();

            writer.WriteLine("Load Cases");
            writer.WriteLine("Id,Name,Type,IncludeSelfWeight,LoadFactor");
            foreach (var lc in model.LoadCases)
                writer.WriteLine($"{lc.CaseId},{lc.Name},{lc.Type},{lc.IncludeSelfWeight},{lc.LoadFactor:F3}");
            writer.WriteLine();
        }

        writer.WriteLine("Node,UX_mm,UY_mm,UZ_mm,RX_rad,RY_rad,RZ_rad,RFx_N,RFy_N,RFz_N,RMx_Nm,RMy_Nm,RMz_Nm");
        foreach (var n in result.NodeResults)
        {
            writer.WriteLine($"{n.NodeId},{n.Displacement.X * 1000:F4},{n.Displacement.Y * 1000:F4},{n.Displacement.Z * 1000:F4},{n.Rotation.X:E4},{n.Rotation.Y:E4},{n.Rotation.Z:E4},{n.ReactionForce.X:F2},{n.ReactionForce.Y:F2},{n.ReactionForce.Z:F2},{n.ReactionMoment.X:F2},{n.ReactionMoment.Y:F2},{n.ReactionMoment.Z:F2}");
        }
        writer.WriteLine();
        writer.WriteLine("Element,Axial_N,ShearY_N,ShearZ_N,Torsion_Nm,MomentY_Nm,MomentZ_Nm,Stress_MPa");
        foreach (var e in result.ElementResults)
        {
            writer.WriteLine($"{e.ElementId},{e.AxialForce:F2},{e.ShearY:F2},{e.ShearZ:F2},{e.Torsion:F2},{e.MomentY:F2},{e.MomentZ:F2},{e.Stress / 1e6:F3}");
        }
        writer.WriteLine();
        writer.WriteLine("Element Stations");
        writer.WriteLine("Element,Station,Axial_N,ShearY_N,ShearZ_N,Torsion_Nm,MomentY_Nm,MomentZ_Nm");
        foreach (var station in result.ElementResults.SelectMany(e => e.StationResults))
        {
            writer.WriteLine($"{station.ElementId},{station.RelativePosition:F2},{station.AxialForce:F2},{station.ShearY:F2},{station.ShearZ:F2},{station.Torsion:F2},{station.MomentY:F2},{station.MomentZ:F2}");
        }
        writer.WriteLine();
        writer.WriteLine("Element,Check,Demand,Capacity,Utilization,Status,Notes");
        foreach (var c in result.DesignChecks)
        {
            writer.WriteLine($"{c.ElementId},{c.CheckType},{c.Demand:E4},{c.Capacity:E4},{c.Utilization:F3},{c.Status},{c.Notes}");
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
        writer.WriteLine($"Max Utilization: {result.SafetyChecks.MaxUtilizationRatio:F3}");
        writer.WriteLine();
        
        writer.WriteLine("--- Node Results ---");
        writer.WriteLine("ID\tδX(mm)\tδY(mm)\tδZ(mm)\tRX(N)\tRY(N)\tRZ(N)");
        foreach (var node in result.Nodes)
        {
            writer.WriteLine($"{node.Id}\t{node.Displacement.X*1000:F3}\t{node.Displacement.Y*1000:F3}\t{node.Displacement.Z*1000:F3}\t{node.ReactionForce.X:F2}\t{node.ReactionForce.Y:F2}\t{node.ReactionForce.Z:F2}");
        }
        writer.WriteLine();
        
        writer.WriteLine("--- Element Results ---");
        writer.WriteLine("ID\tForce(N)\tStress(MPa)\tLength(m)\tUtil.\tStatus");
        foreach (var elem in result.Elements)
        {
            var check = result.SafetyChecks.ElementChecks.FirstOrDefault(c => c.ElementId == elem.Id);
            writer.WriteLine($"{elem.Id}\t{elem.AxialForce:F2}\t{elem.Stress/1e6:F3}\t{elem.Length:F3}\t{check?.UtilizationRatio ?? 0:F3}\t{check?.Status ?? "N/A"}");
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
        if (dgvNodes == null) return;
        int nextId = GetNextGridId(dgvNodes);
        dgvNodes.Rows.Add(nextId, 0, 0, 0, false, false, false, false, false, false, 0, 0, 0, 0, 0, 0);
        UpdateStatus($"Added node {nextId}. Edit coordinates and constraints in the grid.");
    }
    
    private void AddElement()
    {
        AddElementOfType(ElementType.Frame3D);
    }

    private void AddElementOfType(ElementType type)
    {
        if (dgvElements == null || dgvNodes == null) return;
        int nextId = GetNextGridId(dgvElements);
        var nodeRows = dgvNodes.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow && !IsEmptyRow(r)).ToList();
        int startNode = nodeRows.Count > 0 ? Convert.ToInt32(nodeRows[0].Cells["Id"].Value, CultureInfo.InvariantCulture) : 1;
        int endNode = nodeRows.Count > 1 ? Convert.ToInt32(nodeRows[1].Cells["Id"].Value, CultureInfo.InvariantCulture) : startNode;
        dgvElements.Rows.Add(nextId, type.ToString(), 1, 1, startNode, endNode, 0.001, 1e-6, 1e-6, 5e-7, 200e9, 7850, MaterialType.Steel.ToString(), 250e6, 0, false, false, false, false);
        SelectGridRow(new SelectedModelObject { Type = SelectedModelObjectType.Element, Id = nextId, Name = $"Element {nextId}" });
        UpdateStatus($"Added {type} element {nextId}. Edit connectivity and properties in the grid.");
    }

    private void AddNodalLoadFromSelection(SelectedModelObject selection)
    {
        if (dgvLoads == null)
            return;

        int nodeId = selection.Type == SelectedModelObjectType.Node ? selection.Id : GetFirstGridId(dgvNodes);
        string caseId = GetActiveCaseId();
        dgvLoads.Rows.Add("Nodal", caseId, nodeId, "", 0.5, 0, 1, LoadDirection.GlobalZ.ToString(), 0, 0, -1000, 0, 0, 0);
        mainTabs!.SelectedIndex = 0;
        UpdateStatus($"Added nodal load on node {nodeId} in load case {caseId}.");
    }

    private void AddMemberDistributedLoadFromSelection(SelectedModelObject selection)
    {
        if (dgvLoads == null)
            return;

        int elementId = selection.Type == SelectedModelObjectType.Element ? selection.Id : GetFirstGridId(dgvElements);
        string caseId = GetActiveCaseId();
        dgvLoads.Rows.Add("MemberDistributed", caseId, "", elementId, 0.5, 0, 1, LoadDirection.GlobalZ.ToString(), 0, 0, -1000, 0, 0, 0);
        mainTabs!.SelectedIndex = 0;
        UpdateStatus($"Added distributed load on element {elementId} in load case {caseId}.");
    }

    private void DuplicateSelection(SelectedModelObject selection)
    {
        var grid = selection.Type switch
        {
            SelectedModelObjectType.Node => dgvNodes,
            SelectedModelObjectType.Element => dgvElements,
            _ => null
        };
        if (grid == null)
            return;

        var source = FindGridRowById(grid, selection.Id);
        if (source == null)
            return;

        int nextId = GetNextGridId(grid);
        object?[] values = source.Cells.Cast<DataGridViewCell>().Select(c => c.Value).ToArray();
        values[0] = nextId;
        grid.Rows.Add(values);
        SelectGridRow(new SelectedModelObject { Type = selection.Type, Id = nextId, Name = selection.Name });
        UpdateStatus($"Duplicated {selection.Type} {selection.Id} to {nextId}.");
    }

    private void DeleteSelection(SelectedModelObject selection)
    {
        var grid = selection.Type switch
        {
            SelectedModelObjectType.Node => dgvNodes,
            SelectedModelObjectType.Element => dgvElements,
            SelectedModelObjectType.Material => dgvMaterials,
            SelectedModelObjectType.Section => dgvSections,
            _ => null
        };
        if (grid == null)
            return;

        var row = FindGridRowById(grid, selection.Id);
        if (row == null)
            return;

        grid.Rows.Remove(row);
        _structuralModel = BuildStructuralModelFromGrids();
        PopulateObjectTree();
        glView?.SetModel(_structuralModel, _structuralResult);
        UpdateStatus($"Deleted {selection.Type} {selection.Id}.");
    }

    private string GetActiveCaseId()
    {
        string selected = Convert.ToString(activeLoadCaseCombo?.SelectedItem, CultureInfo.InvariantCulture) ?? string.Empty;
        return string.IsNullOrWhiteSpace(selected) || selected == "Default" ? "DEAD" : selected;
    }

    private static int GetFirstGridId(DataGridView? grid)
    {
        if (grid == null)
            return 1;

        foreach (DataGridViewRow row in grid.Rows)
        {
            if (!row.IsNewRow && TryReadInt(row, "Id", out int id))
                return id;
        }

        return 1;
    }

    private static DataGridViewRow? FindGridRowById(DataGridView grid, int id)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (!row.IsNewRow && TryReadInt(row, "Id", out int rowId) && rowId == id)
                return row;
        }

        return null;
    }

    private bool ValidateCurrentModel(bool showDialog)
    {
        try
        {
            _structuralModel = BuildStructuralModelFromGrids();
            var messages = new StructuralSolver(_structuralModel).ValidateModel().ToList();
            PopulateValidationGrid(messages);
            var errors = messages.Where(m => m.Severity == "Error").ToList();
            string message = messages.Count == 0
                ? "Model validation passed."
                : string.Join(Environment.NewLine, messages.Select(m => $"{m.Severity}: {m.Message}"));

            UpdateStatus(message);
            if (showDialog || errors.Count > 0)
            {
                MessageBox.Show(message, "Model Validation", MessageBoxButtons.OK,
                    errors.Count > 0 ? MessageBoxIcon.Error : MessageBoxIcon.Information);
            }

            return errors.Count == 0;
        }
        catch (Exception ex)
        {
            UpdateStatus($"Validation failed: {ex.Message}");
            MessageBox.Show(ex.Message, "Model Validation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void PopulateValidationGrid(IEnumerable<ModelValidationMessage> messages)
    {
        if (dgvValidation == null)
            return;

        dgvValidation.Rows.Clear();
        foreach (var message in messages)
        {
            int rowIndex = dgvValidation.Rows.Add(message.Severity, message.Message, message.ObjectType.ToString(), message.ObjectId);
            var row = dgvValidation.Rows[rowIndex];
            row.DefaultCellStyle.BackColor = message.Severity == "Error"
                ? Color.MistyRose
                : message.Severity == "Warning" ? Color.LemonChiffon : Color.White;
        }
    }

    private void FocusValidationMessage(int rowIndex)
    {
        if (dgvValidation == null || rowIndex < 0 || rowIndex >= dgvValidation.Rows.Count)
            return;

        var row = dgvValidation.Rows[rowIndex];
        if (!Enum.TryParse<SelectedModelObjectType>(Convert.ToString(row.Cells["ObjectType"].Value, CultureInfo.InvariantCulture), out var type) ||
            type == SelectedModelObjectType.None)
        {
            return;
        }

        int id = (int)ReadDoubleOrDefault(row, "ObjectId");
        SelectModelObject(new SelectedModelObject { Type = type, Id = id, Name = $"{type} {id}" }, updateViewer: true);
    }

    private TrussSolver BuildSolverFromGrids()
    {
        var solver = new TrussSolver();
        if (dgvNodes == null || dgvElements == null)
            return solver;

        foreach (DataGridViewRow row in dgvNodes.Rows)
        {
            if (row.IsNewRow || IsEmptyRow(row)) continue;

            var node = new Node(
                ReadInt(row, "Id"),
                new Point3D(ReadDouble(row, "X"), ReadDouble(row, "Y"), ReadDouble(row, "Z")))
            {
                ConstraintX = ReadBool(row, "CX"),
                ConstraintY = ReadBool(row, "CY"),
                ConstraintZ = ReadBool(row, "CZ")
            };
            node.ApplyForce(ReadDouble(row, "FX"), ReadDouble(row, "FY"), ReadDouble(row, "FZ"));
            solver.AddNode(node);
        }

        foreach (DataGridViewRow row in dgvElements.Rows)
        {
            if (row.IsNewRow || IsEmptyRow(row)) continue;

            var material = new Material("Grid Material", ReadDouble(row, "EModulus"), 0.3, ReadDouble(row, "Density"), 250e6);
            solver.AddElement(new Element(
                ReadInt(row, "Id"),
                ReadInt(row, "StartNode"),
                ReadInt(row, "EndNode"),
                ReadDouble(row, "Area"),
                material));
        }

        return solver;
    }

    private StructuralModel BuildStructuralModelFromGrids()
    {
        var model = new StructuralModel();
        if (dgvNodes == null || dgvElements == null)
            return model;

        var elementTypes = dgvElements.Rows.Cast<DataGridViewRow>()
            .Where(r => !r.IsNewRow && !IsEmptyRow(r))
            .Select(r => Enum.TryParse<ElementType>(ReadStringOrDefault(r, "Type", ElementType.Truss.ToString()), out var parsed) ? parsed : ElementType.Truss)
            .ToList();
        bool trussOnly = elementTypes.Count == 0 || elementTypes.All(t => t == ElementType.Truss);

        foreach (DataGridViewRow row in dgvNodes.Rows)
        {
            if (row.IsNewRow || IsEmptyRow(row)) continue;

            var node = new Node(
                ReadInt(row, "Id"),
                new Point3D(ReadDouble(row, "X"), ReadDouble(row, "Y"), ReadDouble(row, "Z")))
            {
                ConstraintX = ReadBool(row, "CX"),
                ConstraintY = ReadBool(row, "CY"),
                ConstraintZ = ReadBool(row, "CZ"),
                ConstraintRX = trussOnly || ReadBoolOrDefault(row, "CRX", false),
                ConstraintRY = trussOnly || ReadBoolOrDefault(row, "CRY", false),
                ConstraintRZ = trussOnly || ReadBoolOrDefault(row, "CRZ", false)
            };
            node.ApplyForce(ReadDoubleOrDefault(row, "FX"), ReadDoubleOrDefault(row, "FY"), ReadDoubleOrDefault(row, "FZ"));
            node.ApplyMoment(ReadDoubleOrDefault(row, "MX"), ReadDoubleOrDefault(row, "MY"), ReadDoubleOrDefault(row, "MZ"));
            model.Nodes.Add(node);
        }

        AddMaterialsFromGrid(model);
        AddSectionsFromGrid(model);

        foreach (DataGridViewRow row in dgvElements.Rows)
        {
            if (row.IsNewRow || IsEmptyRow(row)) continue;

            int id = ReadInt(row, "Id");
            int materialId = (int)ReadDoubleOrDefault(row, "MaterialId", id);
            int sectionId = (int)ReadDoubleOrDefault(row, "SectionId", id);
            EnsureInlineMaterialAndSection(model, row, materialId, sectionId);

            var type = Enum.TryParse<ElementType>(ReadStringOrDefault(row, "Type", ElementType.Truss.ToString()), out var et)
                ? et
                : ElementType.Truss;
            int start = ReadInt(row, "StartNode");
            int end = ReadInt(row, "EndNode");
            var releases = new FrameMemberRelease
            {
                StartMomentY = ReadBoolOrDefault(row, "RelSY", false),
                StartMomentZ = ReadBoolOrDefault(row, "RelSZ", false),
                EndMomentY = ReadBoolOrDefault(row, "RelEY", false),
                EndMomentZ = ReadBoolOrDefault(row, "RelEZ", false)
            };
            double roll = ReadDoubleOrDefault(row, "Roll") * Math.PI / 180.0;
            model.Elements.Add(type == ElementType.Frame3D
                ? new FrameElement3D
                {
                    Id = id,
                    StartNodeId = start,
                    EndNodeId = end,
                    MaterialId = materialId,
                    SectionId = sectionId,
                    RollAngleRadians = roll,
                    Releases = releases
                }
                : new TrussElement
                {
                    Id = id,
                    StartNodeId = start,
                    EndNodeId = end,
                    MaterialId = materialId,
                    SectionId = sectionId,
                    RollAngleRadians = roll,
                    Releases = releases
                });
        }

        AddLoadsFromGrid(model);
        AddCombinationsFromGrid(model);
        model.EnsureDefaultLoadTemplates();
        return model;
    }

    private void AddMaterialsFromGrid(StructuralModel model)
    {
        if (dgvMaterials == null)
            return;

        foreach (DataGridViewRow row in dgvMaterials.Rows)
        {
            if (row.IsNewRow || IsEmptyRow(row)) continue;
            var type = Enum.TryParse<MaterialType>(ReadStringOrDefault(row, "Type", MaterialType.Custom.ToString()), out var parsed)
                ? parsed
                : MaterialType.Custom;
            model.Materials.Add(new Material
            {
                Id = ReadInt(row, "Id"),
                Name = ReadStringOrDefault(row, "Name", $"Material {ReadInt(row, "Id")}"),
                Type = type,
                YoungsModulus = ReadDoubleOrDefault(row, "E", 200e9),
                ShearModulus = ReadDoubleOrDefault(row, "G"),
                PoissonsRatio = ReadDoubleOrDefault(row, "Nu", 0.3),
                Density = ReadDoubleOrDefault(row, "Density", 7850),
                YieldStrength = ReadDoubleOrDefault(row, "Fy", type == MaterialType.Steel ? 250e6 : 0),
                UltimateStrength = ReadDoubleOrDefault(row, "Fu"),
                ConcreteCompressiveStrength = ReadDoubleOrDefault(row, "Fc", type == MaterialType.Concrete ? 30e6 : 0)
            });
        }
    }

    private void AddSectionsFromGrid(StructuralModel model)
    {
        if (dgvSections == null)
            return;

        foreach (DataGridViewRow row in dgvSections.Rows)
        {
            if (row.IsNewRow || IsEmptyRow(row)) continue;
            var type = Enum.TryParse<SectionType>(ReadStringOrDefault(row, "Type", SectionType.Generic.ToString()), out var parsed)
                ? parsed
                : SectionType.Generic;
            model.Sections.Add(new Section
            {
                Id = ReadInt(row, "Id"),
                Name = ReadStringOrDefault(row, "Name", $"Section {ReadInt(row, "Id")}"),
                Type = type,
                Area = ReadDoubleOrDefault(row, "A"),
                Iy = ReadDoubleOrDefault(row, "Iy"),
                Iz = ReadDoubleOrDefault(row, "Iz"),
                J = ReadDoubleOrDefault(row, "J"),
                Width = ReadDoubleOrDefault(row, "Width"),
                Depth = ReadDoubleOrDefault(row, "Depth"),
                RebarArea = ReadDoubleOrDefault(row, "RebarArea"),
                EffectiveDepth = ReadDoubleOrDefault(row, "EffectiveDepth")
            });
        }
    }

    private void EnsureInlineMaterialAndSection(StructuralModel model, DataGridViewRow row, int materialId, int sectionId)
    {
        if (!model.Materials.Any(m => m.Id == materialId))
        {
            var materialType = Enum.TryParse<MaterialType>(ReadStringOrDefault(row, "MaterialType", MaterialType.Steel.ToString()), out var mt)
                ? mt
                : MaterialType.Custom;
            model.Materials.Add(new Material
            {
                Id = materialId,
                Name = $"{materialType} {materialId}",
                Type = materialType,
                YoungsModulus = ReadDouble(row, "EModulus"),
                Density = ReadDouble(row, "Density"),
                PoissonsRatio = 0.3,
                YieldStrength = ReadDoubleOrDefault(row, "Fy", materialType == MaterialType.Steel ? 250e6 : 0),
                ConcreteCompressiveStrength = materialType == MaterialType.Concrete ? 30e6 : 0
            });
        }

        if (!model.Sections.Any(s => s.Id == sectionId))
        {
            double area = ReadDouble(row, "Area");
            model.Sections.Add(new Section
            {
                Id = sectionId,
                Name = $"Section {sectionId}",
                Type = SectionType.Generic,
                Area = area,
                Iy = ReadDoubleOrDefault(row, "Iy", Math.Max(area * 1e-4, 1e-12)),
                Iz = ReadDoubleOrDefault(row, "Iz", Math.Max(area * 1e-4, 1e-12)),
                J = ReadDoubleOrDefault(row, "J", Math.Max(area * 1e-4, 1e-12)),
                Width = Math.Sqrt(area),
                Depth = Math.Sqrt(area)
            });
        }
    }

    private void AddLoadsFromGrid(StructuralModel model)
    {
        if (dgvLoads == null)
            return;

        foreach (DataGridViewRow row in dgvLoads.Rows)
        {
            if (row.IsNewRow || IsEmptyRow(row)) continue;
            string kind = ReadStringOrDefault(row, "Kind", "Nodal");
            string caseId = ReadStringOrDefault(row, "CaseId", "");
            var direction = Enum.TryParse<LoadDirection>(ReadStringOrDefault(row, "Direction", LoadDirection.GlobalZ.ToString()), out var parsedDirection)
                ? parsedDirection
                : LoadDirection.GlobalZ;
            var force = new Vector3D(ReadDoubleOrDefault(row, "FX"), ReadDoubleOrDefault(row, "FY"), ReadDoubleOrDefault(row, "FZ"));
            var moment = new Vector3D(ReadDoubleOrDefault(row, "MX"), ReadDoubleOrDefault(row, "MY"), ReadDoubleOrDefault(row, "MZ"));

            if (kind == "Nodal")
            {
                model.Loads.Add(new NodalLoad { LoadCaseId = caseId, NodeId = (int)ReadDoubleOrDefault(row, "NodeId"), Force = force, Moment = moment });
            }
            else if (kind == "MemberPoint")
            {
                model.Loads.Add(new MemberPointLoad
                {
                    LoadCaseId = caseId,
                    ElementId = (int)ReadDoubleOrDefault(row, "ElementId"),
                    RelativeDistance = ReadDoubleOrDefault(row, "RelativeDistance", 0.5),
                    Direction = direction,
                    Force = force,
                    Moment = moment
                });
            }
            else if (kind == "MemberDistributed")
            {
                model.Loads.Add(new MemberDistributedLoad
                {
                    LoadCaseId = caseId,
                    ElementId = (int)ReadDoubleOrDefault(row, "ElementId"),
                    Direction = direction,
                    ForcePerLength = force,
                    StartRelativeDistance = ReadDoubleOrDefault(row, "StartRelativeDistance", 0.0),
                    EndRelativeDistance = ReadDoubleOrDefault(row, "EndRelativeDistance", 1.0)
                });
            }
        }
    }

    private void AddCombinationsFromGrid(StructuralModel model)
    {
        if (dgvCombinations == null)
            return;

        foreach (DataGridViewRow row in dgvCombinations.Rows)
        {
            if (row.IsNewRow || IsEmptyRow(row)) continue;
            var combination = new LoadCombination
            {
                CombinationId = ReadStringOrDefault(row, "Id", ""),
                Name = ReadStringOrDefault(row, "Name", "")
            };
            foreach (var token in ReadStringOrDefault(row, "Factors", "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = token.Split('=', StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double factor))
                    combination.LoadCases[parts[0]] = factor;
            }
            model.LoadCombinations.Add(combination);
        }
    }

    private static int GetNextGridId(DataGridView grid)
    {
        var ids = grid.Rows.Cast<DataGridViewRow>()
            .Where(r => !r.IsNewRow && r.Cells["Id"].Value != null)
            .Select(r => int.TryParse(Convert.ToString(r.Cells["Id"].Value, CultureInfo.InvariantCulture), out int id) ? id : 0);
        return ids.DefaultIfEmpty(0).Max() + 1;
    }

    private static bool IsEmptyRow(DataGridViewRow row)
    {
        return row.Cells.Cast<DataGridViewCell>().All(c => c.Value == null || string.IsNullOrWhiteSpace(Convert.ToString(c.Value, CultureInfo.InvariantCulture)));
    }

    private static int ReadInt(DataGridViewRow row, string columnName)
    {
        return int.Parse(Convert.ToString(row.Cells[columnName].Value, CultureInfo.InvariantCulture) ?? "0", CultureInfo.InvariantCulture);
    }

    private static double ReadDouble(DataGridViewRow row, string columnName)
    {
        return double.Parse(Convert.ToString(row.Cells[columnName].Value, CultureInfo.InvariantCulture) ?? "0", CultureInfo.InvariantCulture);
    }

    private static double ReadDoubleOrDefault(DataGridViewRow row, string columnName, double defaultValue = 0)
    {
        if (row.DataGridView == null || !row.DataGridView.Columns.Contains(columnName))
            return defaultValue;

        string? text = Convert.ToString(row.Cells[columnName].Value, CultureInfo.InvariantCulture);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : defaultValue;
    }

    private static bool TryReadInt(DataGridViewRow row, string columnName, out int value)
    {
        value = 0;
        if (row.DataGridView == null || !row.DataGridView.Columns.Contains(columnName))
            return false;

        string? text = Convert.ToString(row.Cells[columnName].Value, CultureInfo.InvariantCulture);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool ReadBool(DataGridViewRow row, string columnName)
    {
        object? value = row.Cells[columnName].Value;
        return value is bool b ? b : bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out bool parsed) && parsed;
    }

    private static bool ReadBoolOrDefault(DataGridViewRow row, string columnName, bool defaultValue)
    {
        if (row.DataGridView == null || !row.DataGridView.Columns.Contains(columnName))
            return defaultValue;

        object? value = row.Cells[columnName].Value;
        return value is bool b ? b : bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out bool parsed) ? parsed : defaultValue;
    }

    private static string ReadStringOrDefault(DataGridViewRow row, string columnName, string defaultValue)
    {
        if (row.DataGridView == null || !row.DataGridView.Columns.Contains(columnName))
            return defaultValue;

        string? text = Convert.ToString(row.Cells[columnName].Value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? defaultValue : text;
    }

    private void UpdateStatus(string message)
    {
        if (txtStatus != null)
        {
            txtStatus.Text = message;
        }
    }
}
