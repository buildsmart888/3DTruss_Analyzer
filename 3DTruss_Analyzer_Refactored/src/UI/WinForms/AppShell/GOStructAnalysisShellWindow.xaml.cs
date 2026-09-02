namespace TrussAnalyzer.UI.AppShell;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using TrussAnalyzer.Core.Application;
using TrussAnalyzer.Core.Domain.V1;
using TrussAnalyzer.UI.WinForms;

public partial class GOStructAnalysisShellWindow : Window
{
    private readonly MainForm _legacy = new() { TopLevel = false, FormBorderStyle = Forms.FormBorderStyle.None, Dock = Forms.DockStyle.Fill };
    private readonly ShellViewModel _vm = new();
    public GOStructAnalysisShellWindow()
    {
        InitializeComponent(); DataContext = _vm; Width = _vm.Settings.WindowWidth; Height = _vm.Settings.WindowHeight; LeftColumn.Width = new GridLength(_vm.Settings.LeftPaneWidth); RightColumn.Width = new GridLength(_vm.Settings.RightPaneWidth);
        LegacyHost.Child = _legacy; Loaded += (_, _) => _legacy.Show(); Closed += (_, _) => _legacy.Dispose();
    }
    private void NewRequested(object s, RoutedEventArgs e) => _vm.CreateNew();
    private void OpenRequested(object s, RoutedEventArgs e) { var d = new OpenFileDialog { Filter = "GOStructAnalysis (*.gosa)|*.gosa" }; if (d.ShowDialog(this) == true) _vm.Open(d.FileName); }
    private void SaveRequested(object s, RoutedEventArgs e) { if (_vm.HasPath) _vm.Save(); else SaveAsRequested(s, e); }
    private void SaveAsRequested(object s, RoutedEventArgs e) { var d = new SaveFileDialog { Filter = "GOStructAnalysis (*.gosa)|*.gosa", DefaultExt = ".gosa" }; if (d.ShowDialog(this) == true) _vm.SaveAs(d.FileName); }
    private void RecoverRequested(object s, RoutedEventArgs e) { var d = new OpenFileDialog { Filter = "GOStructAnalysis (*.gosa)|*.gosa" }; if (d.ShowDialog(this) == true) _vm.Recover(d.FileName); }
    private void UndoRequested(object s, RoutedEventArgs e) => _vm.Undo(); private void RedoRequested(object s, RoutedEventArgs e) => _vm.Redo();
    private async void AnalyzeRequested(object s, RoutedEventArgs e) => await _vm.AnalyzeAsync(); private void CancelRequested(object s, RoutedEventArgs e) => _vm.Cancel();
    private void PhysicalRequested(object s, RoutedEventArgs e) => _vm.CurrentStage = "Physical"; private void LoadingRequested(object s, RoutedEventArgs e) => _vm.CurrentStage = "Loading"; private void AnalysisRequested(object s, RoutedEventArgs e) => _vm.CurrentStage = "Analysis"; private void ResultsRequested(object s, RoutedEventArgs e) => _vm.CurrentStage = "Results"; private void DesignRequested(object s, RoutedEventArgs e) => _vm.CurrentStage = "Design"; private void ReportRequested(object s, RoutedEventArgs e) => _vm.CurrentStage = "Report";
    private void AddNodeRequested(object s, RoutedEventArgs e) => _vm.AddNode(); private void AddFrameRequested(object s, RoutedEventArgs e) => _vm.AddMember(false); private void AddTrussRequested(object s, RoutedEventArgs e) => _vm.AddMember(true);
    private void IsoRequested(object s, RoutedEventArgs e) => _vm.SetView("Isometric"); private void PlanRequested(object s, RoutedEventArgs e) => _vm.SetView("Plan XY"); private void LabelRequested(object s, RoutedEventArgs e) => _vm.SetDisplay("Label scale increased"); private void TransparencyRequested(object s, RoutedEventArgs e) => _vm.SetDisplay("Transparency toggled");
    private void ModelSearchChanged(object s, TextChangedEventArgs e) => _vm.FilterModel(((TextBox)s).Text); private void ModelSelectionChanged(object s, SelectionChangedEventArgs e) => _vm.SelectModel(e.AddedItems.OfType<PhysicalTreeItem>().FirstOrDefault());
    private void RecentProjectSelected(object s, SelectionChangedEventArgs e) { if (e.AddedItems.OfType<string>().FirstOrDefault() is { } path) _vm.Open(path); }
    private void ResetLayoutRequested(object s, RoutedEventArgs e) { LeftColumn.Width = new GridLength(220); RightColumn.Width = new GridLength(280); }
    private void LanguageRequested(object s, RoutedEventArgs e) => _vm.ToggleLanguage();
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
    private readonly ApplicationSettingsStore _settingsStore = new(); private readonly ProjectDocumentService _documents = new(); private readonly ProjectCommandHistory _history = new(); private readonly ProjectAnalysisService _analysis = new(); private readonly PhysicalModelEditor _physical = new(); private readonly BackgroundTaskService _background = new(); private readonly AutosaveScheduler _autosave = new(); private CancellationTokenSource? _cts;
    private string _stage; private string _state = "No project document loaded"; private string _analysisStatus = "No analysis requested"; private double _progress;
    public ShellViewModel() { Settings = _settingsStore.Load(_settingsPath); _stage = Settings.ActiveStage; RecentProjects = new(Settings.RecentProjectPaths.Where(File.Exists)); _documents.Changed += (_, _) => Refresh(); _history.Changed += (_, _) => { OnChanged(nameof(CanUndo)); OnChanged(nameof(CanRedo)); }; _autosave.Start(TimeSpan.FromMinutes(Math.Clamp(Settings.AutosaveMinutes, 1, 60)), () => { try { _documents.Autosave(); } catch { } }); }
    public ApplicationSettings Settings { get; private set; } public ObservableCollection<string> RecentProjects { get; } public ObservableCollection<PhysicalTreeItem> VisibleModelItems { get; } = new(); public bool HasPath => _documents.CurrentPath is not null; public bool IsDirty => _documents.IsDirty; public bool CanUndo => _history.CanUndo; public bool CanRedo => _history.CanRedo;
    public string CurrentStage { get => _stage; set { _stage = value; OnChanged(); } } public string ProjectState { get => _state; private set { _state = value; OnChanged(); } } public string AnalysisStatus { get => _analysisStatus; private set { _analysisStatus = value; OnChanged(); } } public double Progress { get => _progress; private set { _progress = value; OnChanged(); OnChanged(nameof(ProgressText)); } } public string ProgressText => Progress is > 0 and < 100 ? $"Working {Progress:0}%" : ""; public string DocumentName => _documents.Current?.ProjectInfo.Name ?? "No active project"; public string DocumentPath => _documents.CurrentPath ?? "Not saved";
    public void CreateNew() { _documents.CreateNew(new ProjectInfo { Name = "Untitled Project" }); _history.Clear(); AnalysisStatus = "New physical model created"; RefreshModelItems(); }
    public void Open(string path) { try { _documents.Open(path); AddRecent(path); AnalysisStatus = "Project opened"; } catch (Exception ex) { AnalysisStatus = ex.Message; } }
    public void Save() { try { _documents.Save(); AddRecent(_documents.CurrentPath!); AnalysisStatus = "Project saved"; } catch (Exception ex) { AnalysisStatus = ex.Message; } }
    public void SaveAs(string path) { try { _documents.SaveAs(path); AddRecent(path); AnalysisStatus = "Project saved"; } catch (Exception ex) { AnalysisStatus = ex.Message; } }
    public void Recover(string path) { try { _documents.Recover(path); AddRecent(_documents.CurrentPath!); AnalysisStatus = "Latest valid snapshot recovered"; } catch (Exception ex) { AnalysisStatus = ex.Message; } }
    public void Undo() { if (CanUndo) _history.Undo(); } public void Redo() { if (CanRedo) _history.Redo(); } public void Cancel() => _cts?.Cancel();
    public void AddNode() { var doc = _documents.Current ?? CreateDocument(); int count = doc.Model.Nodes.Count; ApplyEdit("Add node", value => _physical.AddNode(value, $"N{count + 1}", new Point3DValue(count * 3, 0, 0))); }
    public void AddMember(bool truss) { var doc = _documents.Current ?? CreateDocument(); if (doc.Model.Nodes.Count < 2) { AnalysisStatus = "Add at least two nodes before creating a member."; return; } EnsureStarterProperties(doc); doc = _documents.Current!; var nodes = doc.Model.Nodes; ApplyEdit(truss ? "Add truss" : "Add frame", value => _physical.AddFrame(value, truss ? $"T{value.Model.LineObjects.Count + 1}" : $"F{value.Model.LineObjects.Count + 1}", nodes[^2].Id, nodes[^1].Id, value.Model.Materials[0].Id, value.Model.Sections[0].Id, truss)); }
    public void SetView(string view) { if (_documents.Current is { } doc) _documents.Replace(doc with { PresentationSettings = doc.PresentationSettings with { ActiveView = view } }); AnalysisStatus = $"View: {view}"; }
    public void SetDisplay(string message) => AnalysisStatus = message;
    public void FilterModel(string query) { var doc = _documents.Current; VisibleModelItems.Clear(); if (doc is null) return; foreach (var item in BuildItems(doc).Where(item => item.Label.Contains(query ?? "", StringComparison.OrdinalIgnoreCase))) VisibleModelItems.Add(item); }
    public void SelectModel(PhysicalTreeItem? item) { if (item is not null) AnalysisStatus = $"Selected {item.Kind}: {item.Label}"; }
    public async Task AnalyzeAsync() { var doc = _documents.Current; var pattern = doc?.LoadDefinitions.LoadPatterns.FirstOrDefault(); if (doc is null || pattern is null) { AnalysisStatus = "Open a Model3D project with a load pattern before analysis."; return; } _cts?.Dispose(); _cts = new(); try { AnalysisStatus = "Running preflight..."; var progress = new Progress<double>(v => Progress = v * 100); var result = await _background.RunAsync(_ => _analysis.Analyze(doc, new(ProjectAnalysisSelectionKind.LoadPattern, pattern.Id)), progress, _cts.Token); AnalysisStatus = result.Succeeded ? $"Analysis complete: {result.Snapshot!.SolverName}" : string.Join(" ", result.Preflight.Where(x => x.Severity == "Error").Select(x => x.Message)); if (result.Succeeded) CurrentStage = "Results"; } catch (OperationCanceledException) { AnalysisStatus = "Analysis cancelled."; } finally { Progress = 0; } }
    public void ToggleLanguage() { Settings = Settings with { Language = Settings.Language == "th" ? "en" : "th" }; AnalysisStatus = Settings.Language == "th" ? "ภาษาไทยถูกเลือกสำหรับ shell ใหม่" : "English selected for newly opened shell text."; }
    public void Close(double width, double height, double leftWidth, double rightWidth) { try { _documents.Autosave(); } catch { } Settings = Settings with { LastProjectPath = _documents.CurrentPath, RecentProjectPaths = RecentProjects.ToArray(), ActiveStage = CurrentStage, WindowWidth = width, WindowHeight = height, LeftPaneWidth = leftWidth, RightPaneWidth = rightWidth }; _settingsStore.Save(_settingsPath, Settings); Dispose(); }
    public void Dispose() { _cts?.Cancel(); _cts?.Dispose(); _autosave.Dispose(); }
    private void AddRecent(string p) { var full = Path.GetFullPath(p); foreach (var old in RecentProjects.Where(x => string.Equals(x, full, StringComparison.OrdinalIgnoreCase)).ToArray()) RecentProjects.Remove(old); RecentProjects.Insert(0, full); while (RecentProjects.Count > 10) RecentProjects.RemoveAt(RecentProjects.Count - 1); }
    private ProjectDocument CreateDocument() { CreateNew(); return _documents.Current!; }
    private void EnsureStarterProperties(ProjectDocument doc) { if (doc.Model.Materials.Count > 0 && doc.Model.Sections.Count > 0) return; var model = doc.Model with { Materials = doc.Model.Materials.ToList(), Sections = doc.Model.Sections.ToList() }; if (!model.Materials.Any()) model.Materials.Add(new Material3D { Id = Guid.NewGuid(), Label = "Steel", YoungsModulus = 200e9, ShearModulus = 77e9, PoissonsRatio = .3, Density = 7850 }); if (!model.Sections.Any()) model.Sections.Add(new Section3D { Id = Guid.NewGuid(), Label = "Generic", Area = .01, Iy = 1e-5, Iz = 1e-5, TorsionalConstant = 1e-6 }); _documents.Replace(doc with { Model = model }); }
    private void ApplyEdit(string label, Func<ProjectDocument, ProjectDocument> transform) { var before = _documents.Current ?? throw new InvalidOperationException(); var after = transform(before); _history.Execute(new DelegateProjectCommand(label, () => _documents.Replace(after), () => _documents.Replace(before))); AnalysisStatus = label; }
    private void Refresh() { ProjectState = _documents.Current is null ? "No project document loaded" : _documents.IsDirty ? "Modified · unsaved" : "Saved / current"; RefreshModelItems(); OnChanged(nameof(HasPath)); OnChanged(nameof(IsDirty)); OnChanged(nameof(DocumentName)); OnChanged(nameof(DocumentPath)); }
    private void RefreshModelItems() => FilterModel(string.Empty);
    private static IEnumerable<PhysicalTreeItem> BuildItems(ProjectDocument document) => document.Model.Nodes.Select(node => new PhysicalTreeItem("Node", node.Id, node.Label)).Concat(document.Model.LineObjects.Select(line => new PhysicalTreeItem(line is Truss3D ? "Truss" : "Frame", line.Id, line.Label)));
    public event PropertyChangedEventHandler? PropertyChanged; private void OnChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new(n));
}

public sealed record PhysicalTreeItem(string Kind, Guid Id, string Label);
