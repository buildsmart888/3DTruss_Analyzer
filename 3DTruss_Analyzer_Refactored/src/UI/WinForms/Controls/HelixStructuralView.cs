namespace TrussAnalyzer.UI.WinForms.Controls;

using System.Globalization;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;
using Forms = System.Windows.Forms;
using MediaPoint3D = System.Windows.Media.Media3D.Point3D;
using MediaVector3D = System.Windows.Media.Media3D.Vector3D;
using CorePoint3D = TrussAnalyzer.Core.Models.Point3D;
using CoreVector3D = TrussAnalyzer.Core.Models.Vector3D;

public sealed class HelixStructuralView : Forms.UserControl
{
    private readonly Forms.ToolStrip _toolbar = new() { Dock = Forms.DockStyle.Top, GripStyle = Forms.ToolStripGripStyle.Hidden };
    private readonly ElementHost _host = new() { Dock = Forms.DockStyle.Fill };
    private readonly HelixViewport3D _viewport = new()
    {
        ShowCoordinateSystem = true,
        ShowViewCube = true,
        ZoomExtentsWhenLoaded = true,
        Background = new LinearGradientBrush(Color.FromRgb(222, 240, 253), Colors.White, 90)
    };
    private readonly ModelVisual3D _scene = new();
    private readonly Dictionary<Visual3D, SelectedModelObject> _visualSelections = new();
    private readonly Dictionary<Model3D, SelectedModelObject> _modelSelections = new();
    private readonly Forms.ContextMenuStrip _contextMenu = new();
    private StructuralModel? _model;
    private StructuralAnalysisResult? _result;
    private TrussSolver? _legacySolver;
    private AnalysisResult? _legacyResult;
    private ViewerDisplayOptions _options = new();
    private int _selectedElementId;
    private int _selectedNodeId;
    private SelectedModelObject _currentSelection = SelectedModelObject.None;

    public event EventHandler<SelectedModelObject>? ObjectSelected;
    public event EventHandler<ViewerCommandRequestedEventArgs>? ViewerCommandRequested;

    public HelixStructuralView()
    {
        Dock = Forms.DockStyle.Fill;
        BuildToolbar();
        _host.Child = _viewport;
        Controls.Add(_host);
        Controls.Add(_toolbar);
        _viewport.Children.Add(new SunLight());
        _viewport.Children.Add(_scene);
        _viewport.MouseLeftButtonDown += OnViewportMouseLeftButtonDown;
        _viewport.MouseRightButtonUp += OnViewportMouseRightButtonUp;
        BuildContextMenu();
        SetIsoView();
    }

    public ViewerDisplayOptions DisplayOptions => _options;

    public void SetModel(TrussSolver solver, AnalysisResult? result = null)
    {
        _legacySolver = solver;
        _legacyResult = result;
        _model = StructuralModel.FromTrussSolver(solver);
        _result = null;
        _options = _model.DisplaySettings;
        RefreshView();
    }

    public void SetModel(StructuralModel model, StructuralAnalysisResult? result = null)
    {
        _model = model;
        _result = result;
        _legacySolver = null;
        _legacyResult = null;
        _options = model.DisplaySettings;
        RefreshView();
    }

    public void SelectObject(SelectedModelObject selection)
    {
        _currentSelection = selection;
        _selectedNodeId = selection.Type == SelectedModelObjectType.Node ? selection.Id : 0;
        _selectedElementId = selection.Type == SelectedModelObjectType.Element ? selection.Id : 0;
        RefreshView();
    }

    public void RefreshView()
    {
        _scene.Children.Clear();
        _visualSelections.Clear();
        _modelSelections.Clear();
        if (_model == null || _model.Nodes.Count == 0)
        {
            AddText("No model loaded", new CorePoint3D(0, 0, 0), Brushes.DimGray);
            return;
        }

        if (_options.Layers.Grid)
            AddGrid();
        AddGlobalAxes();
        if (_options.Layers.Elements)
            AddElements();
        if (_options.Layers.DeformedShape && _result != null)
            AddDeformedShape();
        if (_options.Layers.Nodes)
            AddNodes();
        if (_options.Layers.Supports)
            AddSupports();
        if (_options.Layers.Loads)
            AddLoads();
        if (_options.Layers.ReactionLabels && _result != null)
            AddReactionLabels();
        if (_options.Layers.LocalAxes)
            AddLocalAxes();
        AddLegend();
    }

    public void SetDiagramMode(ResultDiagramMode mode)
    {
        _options.DiagramMode = mode;
        RefreshView();
    }

    public void SetLayer(string layerName, bool visible)
    {
        switch (layerName)
        {
            case "Nodes": _options.Layers.Nodes = visible; break;
            case "Elements": _options.Layers.Elements = visible; break;
            case "Supports": _options.Layers.Supports = visible; break;
            case "Loads": _options.Layers.Loads = visible; break;
            case "Load Labels": _options.Layers.LoadLabels = visible; break;
            case "Reaction Labels": _options.Layers.ReactionLabels = visible; break;
            case "Labels": _options.Layers.Labels = visible; break;
            case "Local Axes": _options.Layers.LocalAxes = visible; break;
            case "Deformed Shape": _options.Layers.DeformedShape = visible; break;
            case "Diagrams": _options.Layers.Diagrams = visible; break;
            case "Grid": _options.Layers.Grid = visible; break;
        }
        RefreshView();
    }

    private void BuildToolbar()
    {
        AddButton("Fit", (_, _) => _viewport.ZoomExtents());
        AddButton("Iso", (_, _) => SetIsoView());
        AddButton("Top", (_, _) => SetCamera(new MediaPoint3D(0, 0, 10), new MediaVector3D(0, 1, 0)));
        AddButton("Front", (_, _) => SetCamera(new MediaPoint3D(0, -10, 0), new MediaVector3D(0, 0, 1)));
        AddButton("Side", (_, _) => SetCamera(new MediaPoint3D(10, 0, 0), new MediaVector3D(0, 0, 1)));
        AddToggle("Labels", true, value => _options.Layers.Labels = value);
        AddToggle("Loads", true, value => _options.Layers.Loads = value);
        AddToggle("Load Labels", true, value => _options.Layers.LoadLabels = value);
        AddToggle("Reactions", true, value => _options.Layers.ReactionLabels = value);
        AddToggle("Local Axes", true, value => _options.Layers.LocalAxes = value);
        AddToggle("Deformed", true, value => _options.Layers.DeformedShape = value);
        AddToggle("Diagrams", true, value => _options.Layers.Diagrams = value);

        var modeDrop = new Forms.ToolStripDropDownButton("Mode");
        foreach (ResultDiagramMode mode in Enum.GetValues<ResultDiagramMode>())
        {
            modeDrop.DropDownItems.Add(mode.ToString(), null, (_, _) => SetDiagramMode(mode));
        }
        _toolbar.Items.Add(modeDrop);
    }

    private void BuildContextMenu()
    {
        AddContextCommand("Add Node", "AddNode");
        AddContextCommand("Add Frame Member", "AddFrameMember");
        AddContextCommand("Add Truss Member", "AddTrussMember");
        AddContextCommand("Add Nodal Load", "AddNodalLoad");
        AddContextCommand("Add Member Distributed Load", "AddMemberDistributedLoad");
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());
        AddContextCommand("Duplicate", "Duplicate");
        AddContextCommand("Delete", "Delete");
        AddContextCommand("Show Properties", "ShowProperties");
    }

    private void AddContextCommand(string text, string command)
    {
        _contextMenu.Items.Add(text, null, (_, _) =>
            ViewerCommandRequested?.Invoke(this, new ViewerCommandRequestedEventArgs(command, _currentSelection)));
    }

    private void OnViewportMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var selection = HitTestSelection(e.GetPosition(_viewport));
        if (selection.Type == SelectedModelObjectType.None)
            return;

        SelectObject(selection);
        ObjectSelected?.Invoke(this, selection);
        e.Handled = true;
    }

    private void OnViewportMouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var selection = HitTestSelection(e.GetPosition(_viewport));
        if (selection.Type != SelectedModelObjectType.None)
        {
            SelectObject(selection);
            ObjectSelected?.Invoke(this, selection);
        }

        _contextMenu.Show(this, PointToClient(Forms.Cursor.Position));
        e.Handled = true;
    }

    private SelectedModelObject HitTestSelection(System.Windows.Point position)
    {
        foreach (var hit in Viewport3DHelper.FindHits(_viewport.Viewport, position))
        {
            if (hit.Visual != null && _visualSelections.TryGetValue(hit.Visual, out var visualSelection))
                return visualSelection;
            if (hit.Model != null && _modelSelections.TryGetValue(hit.Model, out var modelSelection))
                return modelSelection;
        }

        return SelectedModelObject.None;
    }

    private void AddButton(string text, EventHandler handler)
    {
        var button = new Forms.ToolStripButton(text);
        button.Click += handler;
        _toolbar.Items.Add(button);
    }

    private void AddToggle(string text, bool initial, Action<bool> changed)
    {
        var button = new Forms.ToolStripButton(text) { CheckOnClick = true, Checked = initial };
        button.CheckedChanged += (_, _) =>
        {
            changed(button.Checked);
            RefreshView();
        };
        _toolbar.Items.Add(button);
    }

    private void SetIsoView() => SetCamera(new MediaPoint3D(8, -8, 6), new MediaVector3D(0, 0, 1));

    private void SetCamera(MediaPoint3D position, MediaVector3D up)
    {
        _viewport.Camera = new PerspectiveCamera
        {
            Position = position,
            LookDirection = new MediaVector3D(-position.X, -position.Y, -position.Z),
            UpDirection = up,
            FieldOfView = 45
        };
        _viewport.ZoomExtents();
    }

    private void AddGrid()
    {
        var (_, span) = GetBounds();
        double size = Math.Max(10, Math.Ceiling(span * 1.8));
        var grid = new GridLinesVisual3D
        {
            Width = size,
            Length = size,
            MajorDistance = 1,
            MinorDistance = 1,
            Thickness = 0.01,
            Fill = Brushes.LightSteelBlue
        };
        _scene.Children.Add(grid);
    }

    private void AddGlobalAxes()
    {
        var (_, span) = GetBounds();
        double length = Math.Max(1.0, span * 0.25);
        AddArrow(new MediaPoint3D(0, 0, 0), new MediaPoint3D(length, 0, 0), Brushes.Red, "+X");
        AddArrow(new MediaPoint3D(0, 0, 0), new MediaPoint3D(0, length, 0), Brushes.Green, "+Y");
        AddArrow(new MediaPoint3D(0, 0, 0), new MediaPoint3D(0, 0, length), Brushes.Blue, "+Z");
    }

    private void AddElements()
    {
        if (_model == null)
            return;

        var nodes = _model.Nodes.ToDictionary(n => n.Id);
        var maxUtil = Math.Max(1e-9, _result?.MaxUtilization ?? 0);
        foreach (var element in _model.Elements)
        {
            if (!nodes.TryGetValue(element.StartNodeId, out var start) || !nodes.TryGetValue(element.EndNodeId, out var end))
                continue;

            var result = GetElementResult(element.Id);
            var brush = GetElementBrush(element, result, maxUtil);
            double diameter = element.Id == _selectedElementId ? 0.075 : 0.045;
            var pipe = new PipeVisual3D
            {
                Point1 = ToMedia(start.Position),
                Point2 = ToMedia(end.Position),
                Diameter = diameter,
                Fill = brush
            };
            AddSelectableVisual(pipe, new SelectedModelObject { Type = SelectedModelObjectType.Element, Id = element.Id, Name = $"E{element.Id}" });

            if (_options.Layers.Labels)
                AddText(GetElementLabel(element, result), Mid(start.Position, end.Position), Brushes.Black);

            if (_options.Layers.Diagrams && result != null && _options.DiagramMode is ResultDiagramMode.ForceDiagram or ResultDiagramMode.MomentDiagram or ResultDiagramMode.Utilization)
                AddResultDiagram(element, start.Position, end.Position, result);
        }
    }

    private void AddNodes()
    {
        if (_model == null)
            return;

        foreach (var node in _model.Nodes)
        {
            var sphere = new SphereVisual3D
            {
                Center = ToMedia(node.Position),
                Radius = node.Id == _selectedNodeId ? 0.11 : 0.075,
                Fill = node.IsConstrained ? Brushes.SeaGreen : Brushes.Black
            };
            AddSelectableVisual(sphere, new SelectedModelObject { Type = SelectedModelObjectType.Node, Id = node.Id, Name = $"N{node.Id}" });
            if (_options.Layers.Labels)
                AddText($"N{node.Id}", Offset(node.Position, 0.1, 0.1, 0.1), Brushes.Black);
        }
    }

    private void AddSupports()
    {
        if (_model == null)
            return;

        foreach (var node in _model.Nodes.Where(n => n.IsConstrained))
        {
            var p = node.Position;
            var support = new BoxVisual3D
            {
                Center = ToMedia(new CorePoint3D(p.X, p.Y, p.Z - 0.08)),
                Width = 0.25,
                Length = 0.25,
                Height = 0.08,
                Fill = Brushes.DarkSlateGray
            };
            AddSelectableVisual(support, new SelectedModelObject { Type = SelectedModelObjectType.Node, Id = node.Id, Name = $"N{node.Id}" });
        }
    }

    private void AddLoads()
    {
        if (_model == null)
            return;

        var nodes = _model.Nodes.ToDictionary(n => n.Id);
        var elements = _model.Elements.ToDictionary(e => e.Id);
        foreach (var node in _model.Nodes)
        {
            if (node.AppliedForce.Magnitude > 1e-9)
                AddLoadArrow(node.Position, node.AppliedForce, LoadLabel($"F {FormatVectorForce(node.AppliedForce)}"));
            if (node.AppliedMoment.Magnitude > 1e-9)
                AddLoadText($"M {FormatVectorMoment(node.AppliedMoment)}", Offset(node.Position, 0, 0, 0.25));
        }

        foreach (var load in _model.Loads)
        {
            switch (load)
            {
                case NodalLoad nodal when nodes.TryGetValue(nodal.NodeId, out var node):
                    if (nodal.Force.Magnitude > 1e-9)
                        AddLoadArrow(node.Position, nodal.Force, LoadLabel($"{nodal.LoadCaseId} {FormatVectorForce(nodal.Force)}"));
                    if (nodal.Moment.Magnitude > 1e-9)
                        AddLoadText($"{nodal.LoadCaseId} M {FormatVectorMoment(nodal.Moment)}", Offset(node.Position, 0, 0, 0.35));
                    break;
                case MemberPointLoad point when elements.TryGetValue(point.ElementId, out var element) &&
                    nodes.TryGetValue(element.StartNodeId, out var start) &&
                    nodes.TryGetValue(element.EndNodeId, out var end):
                    var p = Interpolate(start.Position, end.Position, point.RelativeDistance);
                    if (point.Force.Magnitude > 1e-9)
                        AddLoadArrow(p, point.Force, LoadLabel($"{point.LoadCaseId} P {FormatVectorForce(point.Force)}"));
                    break;
                case MemberDistributedLoad distributed when elements.TryGetValue(distributed.ElementId, out var element) &&
                    nodes.TryGetValue(element.StartNodeId, out var start) &&
                    nodes.TryGetValue(element.EndNodeId, out var end):
                    double a = Math.Clamp(distributed.StartRelativeDistance, 0, 1);
                    double b = Math.Clamp(distributed.EndRelativeDistance, 0, 1);
                    if (b < a)
                        (a, b) = (b, a);
                    for (int i = 1; i <= 3; i++)
                        AddLoadArrow(Interpolate(start.Position, end.Position, a + (b - a) * i / 4.0), distributed.ForcePerLength, string.Empty, 0.35);
                    AddLoadText($"{distributed.LoadCaseId} w {FormatVectorDistributedForce(distributed.ForcePerLength)} [{a:F2}-{b:F2}L]", Offset(Interpolate(start.Position, end.Position, (a + b) / 2), 0, 0, 0.25));
                    break;
            }
        }
    }

    private void AddReactionLabels()
    {
        if (_model == null || _result == null)
            return;

        var nodeMap = _model.Nodes.ToDictionary(n => n.Id);
        foreach (var result in _result.NodeResults)
        {
            if (!nodeMap.TryGetValue(result.NodeId, out var node))
                continue;

            if (result.ReactionForce.Magnitude > 1e-9)
            {
                AddReactionArrow(node.Position, result.ReactionForce);
                AddText($"R {FormatVectorForce(result.ReactionForce)}", Offset(node.Position, 0.12, 0.12, 0.22), Brushes.DarkCyan);
            }

            if (result.ReactionMoment.Magnitude > 1e-9)
                AddText($"RM {FormatVectorMoment(result.ReactionMoment)}", Offset(node.Position, 0.12, -0.12, 0.35), Brushes.DarkCyan);
        }
    }

    private void AddDeformedShape()
    {
        if (_model == null || _result == null || _result.NodeResults.Count == 0)
            return;

        var nodes = _model.Nodes.ToDictionary(n => n.Id);
        var results = _result.NodeResults.ToDictionary(n => n.NodeId);
        var (_, span) = GetBounds();
        double max = Math.Max(1e-12, _result.NodeResults.Max(n => n.Displacement.Magnitude));
        double scale = span * 0.12 / max * _options.DeformationScale;
        var lines = new LinesVisual3D { Color = Colors.Purple, Thickness = 2 };
        foreach (var element in _model.Elements)
        {
            if (!nodes.TryGetValue(element.StartNodeId, out var start) ||
                !nodes.TryGetValue(element.EndNodeId, out var end) ||
                !results.TryGetValue(element.StartNodeId, out var rs) ||
                !results.TryGetValue(element.EndNodeId, out var re))
                continue;

            lines.Points.Add(ToMedia(Offset(start.Position, rs.Displacement.X * scale, rs.Displacement.Y * scale, rs.Displacement.Z * scale)));
            lines.Points.Add(ToMedia(Offset(end.Position, re.Displacement.X * scale, re.Displacement.Y * scale, re.Displacement.Z * scale)));
        }
        _scene.Children.Add(lines);
    }

    private void AddLocalAxes()
    {
        if (_model == null)
            return;

        var nodes = _model.Nodes.ToDictionary(n => n.Id);
        var (_, span) = GetBounds();
        double length = Math.Max(0.25, span * 0.045);
        foreach (var element in _model.Elements)
        {
            if (!nodes.TryGetValue(element.StartNodeId, out var start) || !nodes.TryGetValue(element.EndNodeId, out var end))
                continue;

            var axes = StructuralSolver.GetLocalAxes(start.Position, end.Position, element.RollAngleRadians);
            var mid = Mid(start.Position, end.Position);
            AddLine(mid, Offset(mid, axes.XAxis.X * length, axes.XAxis.Y * length, axes.XAxis.Z * length), Colors.Red, 1);
            AddLine(mid, Offset(mid, axes.YAxis.X * length, axes.YAxis.Y * length, axes.YAxis.Z * length), Colors.Green, 1);
            AddLine(mid, Offset(mid, axes.ZAxis.X * length, axes.ZAxis.Y * length, axes.ZAxis.Z * length), Colors.Blue, 1);
        }
    }

    private void AddResultDiagram(StructuralElement element, CorePoint3D start, CorePoint3D end, ElementForceResult result)
    {
        if (_options.DiagramMode == ResultDiagramMode.Utilization)
        {
            AddUtilizationMarker(element, start, end);
            return;
        }

        if (result.StationResults.Count == 0)
            return;

        var (_, span) = GetBounds();
        double maxValue = result.StationResults
            .Select(GetDiagramStationValue)
            .Select(Math.Abs)
            .DefaultIfEmpty(0)
            .Max();
        if (maxValue <= 1e-9)
            return;

        double maxOffset = Math.Min(span * 0.08, 0.75);
        var axes = StructuralSolver.GetLocalAxes(start, end, element.RollAngleRadians);
        var color = _options.DiagramMode == ResultDiagramMode.MomentDiagram ? Colors.DarkViolet : Colors.DarkOrange;
        var points = result.StationResults
            .OrderBy(s => s.RelativePosition)
            .Select(s =>
            {
                var basePoint = Interpolate(start, end, s.RelativePosition);
                double value = GetDiagramStationValue(s);
                double offset = value / maxValue * maxOffset;
                return Offset(basePoint, axes.ZAxis.X * offset, axes.ZAxis.Y * offset, axes.ZAxis.Z * offset);
            })
            .ToList();

        for (int i = 0; i < points.Count - 1; i++)
            AddLine(points[i], points[i + 1], color, 2);

        var midStation = result.StationResults.OrderBy(s => Math.Abs(s.RelativePosition - 0.5)).First();
        var midPoint = points[points.Count / 2];
        if (_options.Layers.Labels)
            AddText(FormatDiagramValue(midStation), midPoint, new SolidColorBrush(color));
    }

    private void AddUtilizationMarker(StructuralElement element, CorePoint3D start, CorePoint3D end)
    {
        double utilization = _result?.DesignChecks.Where(c => c.ElementId == element.Id).Select(c => c.Utilization).DefaultIfEmpty(0).Max() ?? 0;
        if (utilization <= 1e-9 || !_options.Layers.Labels)
            return;

        AddText($"U={utilization:F2}", Offset(Mid(start, end), 0, 0, 0.18), GetUtilizationBrush(element.Id, Math.Max(1e-9, _result?.MaxUtilization ?? 0)));
    }

    private double GetDiagramStationValue(ElementStationResult station)
    {
        return _options.DiagramMode switch
        {
            ResultDiagramMode.MomentDiagram => Math.Abs(station.MomentZ) >= Math.Abs(station.MomentY) ? station.MomentZ : station.MomentY,
            _ => Math.Abs(station.ShearY) >= Math.Abs(station.ShearZ) ? station.ShearY : station.ShearZ
        };
    }

    private string FormatDiagramValue(ElementStationResult station)
    {
        return _options.DiagramMode switch
        {
            ResultDiagramMode.MomentDiagram => $"M={FormatMoment(GetDiagramStationValue(station))}",
            _ => $"V={FormatForce(GetDiagramStationValue(station))}"
        };
    }

    private void AddLegend()
    {
        if (_model == null)
            return;

        var (center, span) = GetBounds();
        var p = new CorePoint3D(center.X - span * 0.55, center.Y - span * 0.55, center.Z + span * 0.55);
        AddText("Right-handed Z-up | X=red, Y=green, Z=blue | Gravity=-Z", p, Brushes.DimGray);
        if (_result != null)
            AddText($"Result: {_result.LoadCaseName} | Max U={_result.MaxDisplacement * 1000:F3} mm | Max Util={_result.MaxUtilization:F3}", Offset(p, 0, 0, -span * 0.05), Brushes.DimGray);
    }

    private Brush GetElementBrush(StructuralElement element, ElementForceResult? result, double maxUtil)
    {
        if (element.Id == _selectedElementId)
            return Brushes.Gold;
        if (_options.DiagramMode == ResultDiagramMode.Wireframe)
            return Brushes.Transparent;
        if (result == null)
            return Brushes.SteelBlue;

        return _options.DiagramMode switch
        {
            ResultDiagramMode.MomentDiagram when Math.Max(result.MomentY, result.MomentZ) > 1e-9 => Brushes.DarkViolet,
            ResultDiagramMode.Utilization => GetUtilizationBrush(element.Id, maxUtil),
            _ when result.AxialForce > 1e-6 => Brushes.RoyalBlue,
            _ when result.AxialForce < -1e-6 => Brushes.Firebrick,
            _ => Brushes.SlateGray
        };
    }

    private Brush GetUtilizationBrush(int elementId, double maxUtil)
    {
        double utilization = _result?.DesignChecks.Where(c => c.ElementId == elementId).Select(c => c.Utilization).DefaultIfEmpty(0).Max() ?? 0;
        if (utilization > 1.0)
            return Brushes.Firebrick;
        if (utilization > 0.75)
            return Brushes.DarkOrange;
        if (utilization > 0)
            return Brushes.ForestGreen;
        return Brushes.SlateGray;
    }

    private ElementForceResult? GetElementResult(int id)
    {
        if (_result != null)
            return _result.ElementResults.FirstOrDefault(e => e.ElementId == id);
        if (_legacyResult != null)
        {
            var legacy = _legacyResult.Elements.FirstOrDefault(e => e.Id == id);
            if (legacy != null)
                return new ElementForceResult { ElementId = legacy.Id, AxialForce = legacy.AxialForce, Stress = legacy.Stress };
        }
        return null;
    }

    private string GetElementLabel(StructuralElement element, ElementForceResult? result)
    {
        if (result == null)
            return $"E{element.Id}";
        if (element.Type == ElementType.Truss)
            return $"E{element.Id} N={FormatForce(result.AxialForce)}";
        return $"E{element.Id} N={FormatForce(result.AxialForce)} Vy={FormatForce(result.ShearY)} Mz={FormatMoment(result.MomentZ)}";
    }

    private void AddArrow(MediaPoint3D start, MediaPoint3D end, Brush brush, string label)
    {
        _scene.Children.Add(new ArrowVisual3D { Point1 = start, Point2 = end, Diameter = 0.04, Fill = brush });
        AddText(label, new CorePoint3D(end.X, end.Y, end.Z), brush);
    }

    private void AddSelectableVisual(Visual3D visual, SelectedModelObject selection)
    {
        _scene.Children.Add(visual);
        _visualSelections[visual] = selection;
        if (visual is ModelVisual3D modelVisual && modelVisual.Content != null)
            _modelSelections[modelVisual.Content] = selection;
    }

    private void AddLoadArrow(CorePoint3D point, CoreVector3D load, string label, double arrowLength = 0.55)
    {
        if (load.Magnitude <= 1e-9)
            return;

        var (_, span) = GetBounds();
        double length = Math.Max(0.25, span * 0.09) * arrowLength;
        var direction = load.Normalize().Scale(length);
        var end = Offset(point, direction.X, direction.Y, direction.Z);
        AddArrow(ToMedia(point), ToMedia(end), Brushes.DarkOrange, label);
    }

    private void AddReactionArrow(CorePoint3D point, CoreVector3D reaction)
    {
        var (_, span) = GetBounds();
        double length = Math.Max(0.25, span * 0.08);
        var direction = reaction.Normalize().Scale(length);
        AddArrow(ToMedia(point), ToMedia(Offset(point, direction.X, direction.Y, direction.Z)), Brushes.DarkCyan, string.Empty);
    }

    private string LoadLabel(string label) => _options.Layers.LoadLabels ? label : string.Empty;

    private void AddLoadText(string text, CorePoint3D position)
    {
        if (_options.Layers.LoadLabels)
            AddText(text, position, Brushes.DarkOrange);
    }

    private void AddLine(CorePoint3D start, CorePoint3D end, Color color, double thickness)
    {
        var line = new LinesVisual3D { Color = color, Thickness = thickness };
        line.Points.Add(ToMedia(start));
        line.Points.Add(ToMedia(end));
        _scene.Children.Add(line);
    }

    private void AddText(string text, CorePoint3D position, Brush brush)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        _scene.Children.Add(new BillboardTextVisual3D
        {
            Text = text,
            Position = ToMedia(position),
            Foreground = brush,
            Background = Brushes.White
        });
    }

    private static MediaPoint3D ToMedia(CorePoint3D point) => new(point.X, point.Y, point.Z);
    private static CorePoint3D Offset(CorePoint3D point, double x, double y, double z) => new(point.X + x, point.Y + y, point.Z + z);
    private static CorePoint3D Mid(CorePoint3D a, CorePoint3D b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);
    private static CorePoint3D Interpolate(CorePoint3D a, CorePoint3D b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

    private (CorePoint3D Center, double Span) GetBounds()
    {
        var nodes = _model?.Nodes ?? new List<Node>();
        if (nodes.Count == 0)
            return (new CorePoint3D(0, 0, 0), 1);
        double minX = nodes.Min(n => n.Position.X), maxX = nodes.Max(n => n.Position.X);
        double minY = nodes.Min(n => n.Position.Y), maxY = nodes.Max(n => n.Position.Y);
        double minZ = nodes.Min(n => n.Position.Z), maxZ = nodes.Max(n => n.Position.Z);
        double span = Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ);
        return (new CorePoint3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2), Math.Max(1, span));
    }

    private static string FormatForce(double force) => Math.Abs(force) >= 1000 ? $"{force / 1000:F2} kN" : $"{force:F1} N";
    private static string FormatMoment(double moment) => Math.Abs(moment) >= 1000 ? $"{moment / 1000:F2} kN-m" : $"{moment:F1} N-m";
    private static string FormatDistributedForce(double force) => Math.Abs(force) >= 1000 ? $"{force / 1000:F2} kN/m" : $"{force:F1} N/m";

    private static string FormatVectorForce(CoreVector3D force)
    {
        var parts = new List<string>();
        if (Math.Abs(force.X) > 1e-9) parts.Add($"Fx={FormatForce(force.X)}");
        if (Math.Abs(force.Y) > 1e-9) parts.Add($"Fy={FormatForce(force.Y)}");
        if (Math.Abs(force.Z) > 1e-9) parts.Add($"Fz={FormatForce(force.Z)}");
        return parts.Count == 0 ? "0 N" : string.Join(", ", parts);
    }

    private static string FormatVectorMoment(CoreVector3D moment)
    {
        var parts = new List<string>();
        if (Math.Abs(moment.X) > 1e-9) parts.Add($"Mx={FormatMoment(moment.X)}");
        if (Math.Abs(moment.Y) > 1e-9) parts.Add($"My={FormatMoment(moment.Y)}");
        if (Math.Abs(moment.Z) > 1e-9) parts.Add($"Mz={FormatMoment(moment.Z)}");
        return parts.Count == 0 ? "0 N-m" : string.Join(", ", parts);
    }

    private static string FormatVectorDistributedForce(CoreVector3D force)
    {
        var parts = new List<string>();
        if (Math.Abs(force.X) > 1e-9) parts.Add($"wx={FormatDistributedForce(force.X)}");
        if (Math.Abs(force.Y) > 1e-9) parts.Add($"wy={FormatDistributedForce(force.Y)}");
        if (Math.Abs(force.Z) > 1e-9) parts.Add($"wz={FormatDistributedForce(force.Z)}");
        return parts.Count == 0 ? "0 N/m" : string.Join(", ", parts);
    }
}

public sealed class ViewerCommandRequestedEventArgs : EventArgs
{
    public ViewerCommandRequestedEventArgs(string command, SelectedModelObject selection)
    {
        Command = command;
        Selection = selection;
    }

    public string Command { get; }
    public SelectedModelObject Selection { get; }
}
