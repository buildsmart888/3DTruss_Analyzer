namespace TrussAnalyzer.UI.AppShell;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TrussAnalyzer.Core.Application;
using TrussAnalyzer.Core.Domain.V1;
using Forms = System.Windows.Forms;
using TrussAnalyzer.UI.WinForms;

public partial class GOStructAnalysisShellWindow : Window
{
    private readonly MainForm _legacy = new() { TopLevel = false, FormBorderStyle = Forms.FormBorderStyle.None, Dock = Forms.DockStyle.Fill };
    private readonly ShellViewModel _vm = new();
    public GOStructAnalysisShellWindow()
    {
        InitializeComponent(); DataContext = _vm; Width = _vm.Settings.WindowWidth; Height = _vm.Settings.WindowHeight; LeftColumn.Width = new GridLength(_vm.Settings.LeftPaneWidth); RightColumn.Width = new GridLength(_vm.Settings.RightPaneWidth);
        LegacyHost.Child = _legacy; Loaded += (_, _) => _legacy.Show(); Closed += (_, _) => _legacy.Dispose();
        PhysicalViewport.SetDocument(_vm.CurrentDocument);
        PhysicalViewport.ObjectSelected += (_, id) => { _vm.SelectModelById(id); ModelTree.SelectedItem = _vm.SelectedTreeItem; PhysicalViewport.SelectObject(id); };
        PhysicalViewport.PointPlaced += (_, point) => _vm.HandlePointPlaced(point);
        _vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ShellViewModel.CurrentDocument)) { PhysicalViewport.SetDocument(_vm.CurrentDocument); UpdateWorkspace(); } if (e.PropertyName == nameof(ShellViewModel.SelectedObjectId)) PhysicalViewport.SelectObject(_vm.SelectedObjectId); if (e.PropertyName == nameof(ShellViewModel.CurrentStage)) UpdateWorkspace(); if (e.PropertyName is nameof(ShellViewModel.ActiveLoadPatternId) or nameof(ShellViewModel.ShowLoads) or nameof(ShellViewModel.LoadScale)) PhysicalViewport.SetLoadDisplay(_vm.ActiveLoadPatternId, _vm.ShowLoads, _vm.LoadScale, true); };
        UpdateWorkspace();
    }
    private void NewRequested(object s, RoutedEventArgs e) => _vm.CreateNew();
    private void OpenRequested(object s, RoutedEventArgs e) { var d = new OpenFileDialog { Filter = "GOStructAnalysis (*.gosa)|*.gosa" }; if (d.ShowDialog(this) == true) _vm.Open(d.FileName); }
    private void SaveRequested(object s, RoutedEventArgs e) { if (_vm.HasPath) _vm.Save(); else SaveAsRequested(s, e); }
    private void SaveAsRequested(object s, RoutedEventArgs e) { var d = new SaveFileDialog { Filter = "GOStructAnalysis (*.gosa)|*.gosa", DefaultExt = ".gosa" }; if (d.ShowDialog(this) == true) _vm.SaveAs(d.FileName); }
    private void RecoverRequested(object s, RoutedEventArgs e) { var d = new OpenFileDialog { Filter = "GOStructAnalysis (*.gosa)|*.gosa" }; if (d.ShowDialog(this) == true) _vm.Recover(d.FileName); }
    private void UndoRequested(object s, RoutedEventArgs e) => _vm.Undo(); private void RedoRequested(object s, RoutedEventArgs e) => _vm.Redo();
    private async void AnalyzeRequested(object s, RoutedEventArgs e) => await _vm.AnalyzeAsync(); private void CancelRequested(object s, RoutedEventArgs e) => _vm.Cancel();
    private void PhysicalRequested(object s, RoutedEventArgs e) => _vm.CurrentStage = "Physical"; private void LoadingRequested(object s, RoutedEventArgs e) => _vm.CurrentStage = "Loading"; private void AnalysisRequested(object s, RoutedEventArgs e) => _vm.CurrentStage = "Analysis"; private void ResultsRequested(object s, RoutedEventArgs e) => _vm.CurrentStage = "Results"; private void DesignRequested(object s, RoutedEventArgs e) => _vm.CurrentStage = "Design"; private void ReportRequested(object s, RoutedEventArgs e) => _vm.CurrentStage = "Report";
    private void AddNodeRequested(object s, RoutedEventArgs e) { PhysicalViewport.BeginPlacement(PlacementMode.Node); _vm.AnalysisStatusText = "Click the work plane to place a node."; }
    private void AddFrameRequested(object s, RoutedEventArgs e) { _vm.BeginMemberPlacement(false); PhysicalViewport.BeginPlacement(PlacementMode.MemberStart); }
    private void AddTrussRequested(object s, RoutedEventArgs e) { _vm.BeginMemberPlacement(true); PhysicalViewport.BeginPlacement(PlacementMode.MemberStart); }
    private void CreateGroupRequested(object s, RoutedEventArgs e) => _vm.CreateGroupFromSelection(); private void ColorGroupRequested(object s, RoutedEventArgs e) => _vm.CycleSelectedGroupColor();
    private void LoadPatternRequested(object s, RoutedEventArgs e) => _vm.ApplyThaiTemplates(); private void LoadFilterRequested(object s, RoutedEventArgs e) => _vm.CycleLoadPattern(); private void LoadScaleRequested(object s, RoutedEventArgs e) => _vm.IncreaseLoadScale(); private void ToggleLoadsRequested(object s, RoutedEventArgs e) => _vm.ToggleLoads();
    private void IsoRequested(object s, RoutedEventArgs e) => _vm.SetView("Isometric"); private void PlanRequested(object s, RoutedEventArgs e) => _vm.SetView("Plan XY"); private void LabelRequested(object s, RoutedEventArgs e) => _vm.IncreaseLabelScale(); private void TransparencyRequested(object s, RoutedEventArgs e) => _vm.ToggleTransparency(); private void ApplyNodeRequested(object s, RoutedEventArgs e) => _vm.ApplySelectedNode();
    private void ModelSearchChanged(object s, TextChangedEventArgs e) => _vm.FilterModel(((TextBox)s).Text); private void ModelSelectionChanged(object s, SelectionChangedEventArgs e) { var item = e.AddedItems.OfType<PhysicalTreeItem>().FirstOrDefault(); _vm.SelectModel(item); PhysicalViewport.SelectObject(item?.Id); }
    private void RecentProjectSelected(object s, SelectionChangedEventArgs e) { if (e.AddedItems.OfType<string>().FirstOrDefault() is { } path) _vm.Open(path); }
    private void ResetLayoutRequested(object s, RoutedEventArgs e) { LeftColumn.Width = new GridLength(220); RightColumn.Width = new GridLength(280); }
    private void LanguageRequested(object s, RoutedEventArgs e) => _vm.ToggleLanguage();
    private void UpdateWorkspace()
    {
        bool physical = string.Equals(_vm.CurrentStage, "Physical", StringComparison.OrdinalIgnoreCase);
        PhysicalViewport.Visibility = physical ? Visibility.Visible : Visibility.Collapsed;
        LegacyHost.Visibility = physical ? Visibility.Collapsed : Visibility.Visible;
        EmptyModelOverlay.Visibility = physical && _vm.CurrentDocument is null ? Visibility.Visible : Visibility.Collapsed;
    }
    private void WindowClosing(object? s, CancelEventArgs e)
    {
        if (_vm.IsDirty)
        {
            var response = MessageBox.Show("The project has unsaved changes. Close without saving?", "GOStructAnalysis", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (response != MessageBoxResult.Yes) { e.Cancel = true; return; }
        }
        _vm.Close(ActualWidth, ActualHeight, LeftColumn.ActualWidth, RightColumn.ActualWidth);
    }
}

public sealed class ShellViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GOStructAnalysis", "settings.json");
    private readonly ApplicationSettingsStore _settingsStore = new(); private readonly ProjectDocumentService _documents = new(); private readonly ProjectCommandHistory _history = new(); private readonly ProjectAnalysisService _analysis = new(); private readonly PhysicalModelEditor _physical = new(); private readonly PhysicalModelSnapper _snapper = new(); private readonly BackgroundTaskService _background = new(); private readonly AutosaveScheduler _autosave = new(); private CancellationTokenSource? _cts;
    private string _stage; private string _state = "No project document loaded"; private string _analysisStatus = "No analysis requested"; private double _progress; private PhysicalTreeItem? _selectedItem; private string _selectedNodeLabel = ""; private string _selectedNodeX = ""; private string _selectedNodeY = ""; private string _selectedNodeZ = "";
    public ShellViewModel() { Settings = _settingsStore.Load(_settingsPath); _stage = Settings.ActiveStage; RecentProjects = new(Settings.RecentProjectPaths.Where(File.Exists)); _documents.Changed += (_, _) => Refresh(); _history.Changed += (_, _) => { OnChanged(nameof(CanUndo)); OnChanged(nameof(CanRedo)); }; _autosave.Start(TimeSpan.FromMinutes(Math.Clamp(Settings.AutosaveMinutes, 1, 60)), () => { try { _documents.Autosave(); } catch { } }); }
    public ApplicationSettings Settings { get; private set; } public ObservableCollection<string> RecentProjects { get; } public ObservableCollection<PhysicalTreeItem> VisibleModelItems { get; } = new(); public ProjectDocument? CurrentDocument => _documents.Current; public Guid? SelectedObjectId => _selectedItem?.Id; public PhysicalTreeItem? SelectedTreeItem => _selectedItem; public bool HasPath => _documents.CurrentPath is not null; public bool IsDirty => _documents.IsDirty; public bool CanUndo => _history.CanUndo; public bool CanRedo => _history.CanRedo;
    public string CurrentStage { get => _stage; set { _stage = value; OnChanged(); } } public string ProjectState { get => _state; private set { _state = value; OnChanged(); } } public string AnalysisStatus { get => _analysisStatus; private set { _analysisStatus = value; OnChanged(); } } public double Progress { get => _progress; private set { _progress = value; OnChanged(); OnChanged(nameof(ProgressText)); } } public string ProgressText => Progress is > 0 and < 100 ? $"Working {Progress:0}%" : ""; public string DocumentName => _documents.Current?.ProjectInfo.Name ?? "No active project"; public string DocumentPath => _documents.CurrentPath ?? "Not saved";
    public string SelectedKind => _selectedItem?.Kind ?? "Nothing selected"; public string SelectedId => _selectedItem?.Id.ToString("D") ?? "Select an object in the tree"; public bool CanEditSelectedNode => _selectedItem?.Kind is "Node" or "Nodal Load" or "Member Load";
    public string SelectedNodeLabel { get => _selectedNodeLabel; set { _selectedNodeLabel = value; OnChanged(); } } public string SelectedNodeX { get => _selectedNodeX; set { _selectedNodeX = value; OnChanged(); } } public string SelectedNodeY { get => _selectedNodeY; set { _selectedNodeY = value; OnChanged(); } } public string SelectedNodeZ { get => _selectedNodeZ; set { _selectedNodeZ = value; OnChanged(); } }
    public void CreateNew() { _documents.CreateNew(new ProjectInfo { Name = "Untitled Project" }); _history.Clear(); AnalysisStatus = "New physical model created"; RefreshModelItems(); }
    public void Open(string path) { try { _documents.Open(path); AddRecent(path); AnalysisStatus = "Project opened"; } catch (Exception ex) { AnalysisStatus = ex.Message; } }
    public void Save() { try { _documents.Save(); AddRecent(_documents.CurrentPath!); AnalysisStatus = "Project saved"; } catch (Exception ex) { AnalysisStatus = ex.Message; } }
    public void SaveAs(string path) { try { _documents.SaveAs(path); AddRecent(path); AnalysisStatus = "Project saved"; } catch (Exception ex) { AnalysisStatus = ex.Message; } }
    public void Recover(string path) { try { _documents.Recover(path); AddRecent(_documents.CurrentPath!); AnalysisStatus = "Latest valid snapshot recovered"; } catch (Exception ex) { AnalysisStatus = ex.Message; } }
    public void Undo() { if (CanUndo) _history.Undo(); } public void Redo() { if (CanRedo) _history.Redo(); } public void Cancel() => _cts?.Cancel();
    private bool _pendingTruss; private Guid? _pendingMemberStart;
    private Guid? _activeLoadPatternId; private bool _showLoads = true; private double _loadScale = .001;
    public Guid? ActiveLoadPatternId { get => _activeLoadPatternId; private set { _activeLoadPatternId = value; OnChanged(); } }
    public bool ShowLoads { get => _showLoads; private set { _showLoads = value; OnChanged(); } }
    public double LoadScale { get => _loadScale; private set { _loadScale = value; OnChanged(); } }
    public string AnalysisStatusText { get => AnalysisStatus; set => AnalysisStatus = value; }
    public void ApplyThaiTemplates() { var doc = _documents.Current ?? CreateDocument(); _documents.Replace(new ThaiModel3DTemplateService().Apply(doc)); Refresh(); AnalysisStatus = "Thai load templates applied (PRELIMINARY)."; }
    public void CycleLoadPattern() { var patterns = _documents.Current?.LoadDefinitions.LoadPatterns ?? new(); if (patterns.Count == 0) { AnalysisStatus = "No load patterns available."; return; } var index = ActiveLoadPatternId is null ? -1 : patterns.FindIndex(item => item.Id == ActiveLoadPatternId); ActiveLoadPatternId = patterns[(index + 1) % patterns.Count].Id; AnalysisStatus = $"Load filter: {patterns[(index + 1) % patterns.Count].Label}"; }
    public void IncreaseLoadScale() { LoadScale = LoadScale >= .01 ? .001 : LoadScale * 2; AnalysisStatus = $"Load display scale: {LoadScale:G3}"; }
    public void ToggleLoads() { ShowLoads = !ShowLoads; AnalysisStatus = ShowLoads ? "Loads visible." : "Loads hidden."; }
    public void AddNode() { var doc = _documents.Current ?? CreateDocument(); int count = doc.Model.Nodes.Count; var snapped = _snapper.Snap(doc, new Point3DValue(count * 3, 0, 0)); AddNodeAt(snapped.Position, snapped.Kind); }
    public void BeginMemberPlacement(bool truss) { _pendingTruss = truss; _pendingMemberStart = null; AnalysisStatus = $"Click the first node or work-plane point for a {(truss ? "truss" : "frame")}."; }
    public void HandlePointPlaced(Point3DValue point)
    {
        var doc = _documents.Current ?? CreateDocument();
        var snapped = _snapper.Snap(doc, point);
        if (_pendingMemberStart is null)
        {
            var id = snapped.EndpointNodeId ?? Guid.NewGuid();
            if (snapped.EndpointNodeId is null)
                ApplyEdit("Place member start", value => _physical.AddNode(value, $"N{doc.Model.Nodes.Count + 1}", snapped.Position, id));
            _pendingMemberStart = id; AnalysisStatus = "Start placed. Click the second point."; return;
        }
        var endId = snapped.EndpointNodeId ?? Guid.NewGuid();
        ApplyEdit("Place member", value => { var withEnd = snapped.EndpointNodeId is null ? _physical.AddNode(value, $"N{value.Model.Nodes.Count + 1}", snapped.Position, endId) : value; var prepared = EnsureStarterProperties(withEnd); var nodes = prepared.Model.Nodes; return _physical.AddFrame(prepared, _pendingTruss ? $"T{prepared.Model.LineObjects.Count + 1}" : $"F{prepared.Model.LineObjects.Count + 1}", _pendingMemberStart.Value, endId, prepared.Model.Materials[0].Id, prepared.Model.Sections[0].Id, _pendingTruss); });
        _pendingMemberStart = null; AnalysisStatus = $"{(_pendingTruss ? "Truss" : "Frame")} placed ({snapped.Kind.ToString().ToLowerInvariant()} snap).";
    }
    private void AddNodeAt(Point3DValue position, PhysicalSnapKind kind) { var doc = _documents.Current ?? CreateDocument(); int count = doc.Model.Nodes.Count; ApplyEdit("Add node", value => _physical.AddNode(value, $"N{count + 1}", position)); AnalysisStatus = $"Add node ({kind.ToString().ToLowerInvariant()} snap)"; }
    public void AddMember(bool truss) { var doc = _documents.Current ?? CreateDocument(); if (doc.Model.Nodes.Count < 2) { AnalysisStatus = "Add at least two nodes before creating a member."; return; } ApplyEdit(truss ? "Add truss" : "Add frame", value => { var prepared = EnsureStarterProperties(value); var nodes = prepared.Model.Nodes; return _physical.AddFrame(prepared, truss ? $"T{prepared.Model.LineObjects.Count + 1}" : $"F{prepared.Model.LineObjects.Count + 1}", nodes[^2].Id, nodes[^1].Id, prepared.Model.Materials[0].Id, prepared.Model.Sections[0].Id, truss); }); }
    public void SetView(string view) { if (_documents.Current is { } doc) _documents.Replace(doc with { PresentationSettings = doc.PresentationSettings with { ActiveView = view } }); AnalysisStatus = $"View: {view}"; }
    public void IncreaseLabelScale() { if (_documents.Current is { } doc) { var next = doc.PresentationSettings.LabelScale >= 2 ? 1 : doc.PresentationSettings.LabelScale + .25; _documents.Replace(doc with { PresentationSettings = doc.PresentationSettings with { LabelScale = next } }); AnalysisStatus = $"Label scale: {next:0.00}x"; } }
    public void ToggleTransparency() { if (_documents.Current is { } doc) { var next = doc.PresentationSettings.Transparency < .5 ? .65 : 0; _documents.Replace(doc with { PresentationSettings = doc.PresentationSettings with { Transparency = next } }); AnalysisStatus = next > 0 ? "Transparent display on" : "Opaque display on"; } }
    public void FilterModel(string query) { var doc = _documents.Current; VisibleModelItems.Clear(); if (doc is null) return; foreach (var item in BuildItems(doc).Where(item => item.Label.Contains(query ?? "", StringComparison.OrdinalIgnoreCase))) VisibleModelItems.Add(item); }
    public void SelectModel(PhysicalTreeItem? item) { _selectedItem = item; if (item is not null && item.Kind == "Node" && _documents.Current?.Model.Nodes.FirstOrDefault(node => node.Id == item.Id) is { } node) { SelectedNodeLabel = node.Label; SelectedNodeX = node.Position.X.ToString(System.Globalization.CultureInfo.InvariantCulture); SelectedNodeY = node.Position.Y.ToString(System.Globalization.CultureInfo.InvariantCulture); SelectedNodeZ = node.Position.Z.ToString(System.Globalization.CultureInfo.InvariantCulture); } else if (item is not null && _documents.Current?.LoadDefinitions.Assignments.FirstOrDefault(load => load.Id == item.Id) is { } assignment) { var editor = LoadAssignmentEditor.From(assignment); SelectedNodeLabel = editor.Label; SelectedNodeX = editor.X.ToString(System.Globalization.CultureInfo.InvariantCulture); SelectedNodeY = editor.Y.ToString(System.Globalization.CultureInfo.InvariantCulture); SelectedNodeZ = editor.Z.ToString(System.Globalization.CultureInfo.InvariantCulture); } OnChanged(nameof(SelectedKind)); OnChanged(nameof(SelectedId)); OnChanged(nameof(SelectedObjectId)); OnChanged(nameof(CanEditSelectedNode)); if (item is not null) AnalysisStatus = $"Selected {item.Kind}: {item.Label}"; }
    public void SelectModelById(Guid id) => SelectModel(VisibleModelItems.FirstOrDefault(item => item.Id == id) ?? BuildItems(_documents.Current!).FirstOrDefault(item => item.Id == id));
    public void ApplySelectedNode() { if (_selectedItem is null) return; if (!double.TryParse(SelectedNodeX, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) || !double.TryParse(SelectedNodeY, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) || !double.TryParse(SelectedNodeZ, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z)) { AnalysisStatus = "Values must be valid invariant numbers."; return; } try { var id = _selectedItem.Id; if (_selectedItem.Kind == "Node") ApplyEdit("Edit node", document => _physical.UpdateNode(document, id, SelectedNodeLabel, new(x, y, z))); else if (_documents.Current?.LoadDefinitions.Assignments.Any(load => load.Id == id) == true) ApplyEdit("Edit load", document => new LoadWorkspaceService().UpdateAssignment(document, id, editor => { editor.Label = SelectedNodeLabel; editor.X = x; editor.Y = y; editor.Z = z; })); } catch (Exception ex) { AnalysisStatus = ex.Message; } }
    public void CreateGroupFromSelection() { if (_selectedItem is null || _selectedItem.Kind == "Group") { AnalysisStatus = "Select a node, frame, or truss before creating a group."; return; } ApplyEdit("Create group", document => _physical.CreateGroup(document, $"Group {document.Model.Groups.Count + 1}", new[] { _selectedItem.Id })); }
    public void CycleSelectedGroupColor() { if (_selectedItem is null) { AnalysisStatus = "Select a group or a grouped object first."; return; } var document = _documents.Current; var group = _selectedItem.Kind == "Group" ? document?.Model.Groups.FirstOrDefault(value => value.Id == _selectedItem.Id) : document?.Model.Groups.FirstOrDefault(value => value.ObjectIds.Contains(_selectedItem.Id)); if (group is null) { AnalysisStatus = "The selected object is not in a group."; return; } var palette = new[] { "#E66928", "#137C8B", "#805AD5", "#3D8B40" }; var current = document!.PresentationSettings.GroupDisplayColors.TryGetValue(group.Id, out var value) ? value : ""; var index = Array.IndexOf(palette, current); var next = palette[(index + 1) % palette.Length]; ApplyEdit("Set group colour", value => _physical.SetGroupDisplayColor(value, group.Id, next)); }
    public async Task AnalyzeAsync() { var doc = _documents.Current; var pattern = doc?.LoadDefinitions.LoadPatterns.FirstOrDefault(); if (doc is null || pattern is null) { AnalysisStatus = "Open a Model3D project with a load pattern before analysis."; return; } _cts?.Dispose(); _cts = new(); try { AnalysisStatus = "Running preflight..."; var progress = new Progress<double>(v => Progress = v * 100); var result = await _background.RunAsync(_ => _analysis.Analyze(doc, new(ProjectAnalysisSelectionKind.LoadPattern, pattern.Id)), progress, _cts.Token); AnalysisStatus = result.Succeeded ? $"Analysis complete: {result.Snapshot!.SolverName}" : string.Join(" ", result.Preflight.Where(x => x.Severity == "Error").Select(x => x.Message)); if (result.Succeeded) CurrentStage = "Results"; } catch (OperationCanceledException) { AnalysisStatus = "Analysis cancelled."; } finally { Progress = 0; } }
    public void ToggleLanguage() { Settings = Settings with { Language = Settings.Language == "th" ? "en" : "th" }; AnalysisStatus = Settings.Language == "th" ? "ภาษาไทยถูกเลือกสำหรับ shell ใหม่" : "English selected for newly opened shell text."; }
    public void Close(double width, double height, double leftWidth, double rightWidth) { try { _documents.Autosave(); } catch { } Settings = Settings with { LastProjectPath = _documents.CurrentPath, RecentProjectPaths = RecentProjects.ToArray(), ActiveStage = CurrentStage, WindowWidth = width, WindowHeight = height, LeftPaneWidth = leftWidth, RightPaneWidth = rightWidth }; _settingsStore.Save(_settingsPath, Settings); Dispose(); }
    public void Dispose() { _cts?.Cancel(); _cts?.Dispose(); _autosave.Dispose(); }
    private void AddRecent(string p) { var full = Path.GetFullPath(p); foreach (var old in RecentProjects.Where(x => string.Equals(x, full, StringComparison.OrdinalIgnoreCase)).ToArray()) RecentProjects.Remove(old); RecentProjects.Insert(0, full); while (RecentProjects.Count > 10) RecentProjects.RemoveAt(RecentProjects.Count - 1); }
    private ProjectDocument CreateDocument() { CreateNew(); return _documents.Current!; }
    private static ProjectDocument EnsureStarterProperties(ProjectDocument doc) { if (doc.Model.Materials.Count > 0 && doc.Model.Sections.Count > 0) return doc; var model = doc.Model with { Materials = doc.Model.Materials.ToList(), Sections = doc.Model.Sections.ToList() }; if (!model.Materials.Any()) model.Materials.Add(new Material3D { Id = Guid.NewGuid(), Label = "Steel", YoungsModulus = 200e9, ShearModulus = 77e9, PoissonsRatio = .3, Density = 7850 }); if (!model.Sections.Any()) model.Sections.Add(new Section3D { Id = Guid.NewGuid(), Label = "Generic", Area = .01, Iy = 1e-5, Iz = 1e-5, TorsionalConstant = 1e-6 }); return doc with { Model = model, AuditMetadata = doc.AuditMetadata with { ModifiedUtc = DateTimeOffset.UtcNow } }; }
    private void ApplyEdit(string label, Func<ProjectDocument, ProjectDocument> transform) { var before = _documents.Current ?? throw new InvalidOperationException(); var after = transform(before); _history.Execute(new DelegateProjectCommand(label, () => _documents.Replace(after), () => _documents.Replace(before))); AnalysisStatus = label; }
    private void Refresh() { ProjectState = _documents.Current is null ? "No project document loaded" : _documents.IsDirty ? "Modified · unsaved" : "Saved / current"; RefreshModelItems(); OnChanged(nameof(CurrentDocument)); OnChanged(nameof(HasPath)); OnChanged(nameof(IsDirty)); OnChanged(nameof(DocumentName)); OnChanged(nameof(DocumentPath)); }
    private void RefreshModelItems() => FilterModel(string.Empty);
    private static IEnumerable<PhysicalTreeItem> BuildItems(ProjectDocument document) => document.Model.Nodes.Select(node => new PhysicalTreeItem("Node", node.Id, node.Label)).Concat(document.Model.LineObjects.Select(line => new PhysicalTreeItem(line is Truss3D ? "Truss" : "Frame", line.Id, line.Label))).Concat(document.Model.Groups.Select(group => new PhysicalTreeItem("Group", group.Id, group.Label))).Concat(document.LoadDefinitions.Assignments.Select(load => new PhysicalTreeItem(load is NodalLoadAssignment3D ? "Nodal Load" : "Member Load", load.Id, load.Label)));
    public event PropertyChangedEventHandler? PropertyChanged; private void OnChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new(n));
}

public sealed record PhysicalTreeItem(string Kind, Guid Id, string Label);
