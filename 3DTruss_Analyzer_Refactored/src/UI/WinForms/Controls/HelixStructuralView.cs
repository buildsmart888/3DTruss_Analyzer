namespace TrussAnalyzer.UI.WinForms.Controls;

using System.Globalization;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;
using TrussAnalyzer.Core.Visualization;
using Forms = System.Windows.Forms;
using MediaPoint3D = System.Windows.Media.Media3D.Point3D;
using MediaVector3D = System.Windows.Media.Media3D.Vector3D;
using CorePoint3D = TrussAnalyzer.Core.Models.Point3D;
using CoreVector3D = TrussAnalyzer.Core.Models.Vector3D;

public sealed class HelixStructuralView : Forms.UserControl
{
    private const double MinimumDiagramScale = 0.25;
    private const double MaximumDiagramScale = 4.0;

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
        AddToggle("Real Sections", false, value => _options.Layers.RealSectionShapes = value);
        AddToggle("Deformed", true, value => _options.Layers.DeformedShape = value);
        AddToggle("Diagrams", true, value => _options.Layers.Diagrams = value);
        AddToggle("Extrema", true, value => _options.ShowDiagramExtrema = value);
        AddButton("Scale -", (_, _) => SetDiagramScale(_options.DiagramScale / 1.25));
        AddButton("Scale +", (_, _) => SetDiagramScale(_options.DiagramScale * 1.25));
        AddButton("Auto Scale", (_, _) => SetDiagramScale(1.0));

        var modeDrop = new Forms.ToolStripDropDownButton("Diagram");
        foreach (var mode in GetToolbarDiagramModes())
        {
            modeDrop.DropDownItems.Add(GetDiagramModeName(mode), null, (_, _) => SetDiagramMode(mode));
        }
        _toolbar.Items.Add(modeDrop);
    }

    private void SetDiagramScale(double scale)
    {
        _options.DiagramScale = Math.Clamp(scale, MinimumDiagramScale, MaximumDiagramScale);
        RefreshView();
    }

    private static IEnumerable<ResultDiagramMode> GetToolbarDiagramModes()
    {
        yield return ResultDiagramMode.Rendered;
        yield return ResultDiagramMode.Wireframe;
        yield return ResultDiagramMode.Deformed;
        yield return ResultDiagramMode.AxialForce;
        yield return ResultDiagramMode.ShearY;
        yield return ResultDiagramMode.ShearZ;
        yield return ResultDiagramMode.Torsion;
        yield return ResultDiagramMode.MomentY;
        yield return ResultDiagramMode.MomentZ;
        yield return ResultDiagramMode.Utilization;
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
            if (_options.Layers.RealSectionShapes && TryAddRealSectionVisual(element, start.Position, end.Position, brush))
            {
                if (element.Id == _selectedElementId)
                    AddLine(start.Position, end.Position, Colors.Gold, 5);
            }
            else
            {
                var pipe = new PipeVisual3D
                {
                    Point1 = ToMedia(start.Position),
                    Point2 = ToMedia(end.Position),
                    Diameter = diameter,
                    Fill = brush
                };
                AddSelectableVisual(pipe, new SelectedModelObject { Type = SelectedModelObjectType.Element, Id = element.Id, Name = $"E{element.Id}" });
            }

            if (_options.Layers.Labels)
                AddText(GetElementLabel(element, result), Mid(start.Position, end.Position), Brushes.Black);

            if (_options.Layers.Diagrams && result != null && IsResultDiagramMode(_options.DiagramMode))
                AddResultDiagram(element, start.Position, end.Position, result);
        }
    }

    private bool TryAddRealSectionVisual(StructuralElement element, CorePoint3D start, CorePoint3D end, Brush brush)
    {
        if (_model == null)
            return false;

        var section = _model.Sections.FirstOrDefault(s => s.Id == element.SectionId);
        if (section == null)
            return false;

        var profile = SectionVisualProfileService.Create(section);
        if (!profile.HasGeometry)
            return false;

        double scale = Math.Max(0.01, _options.SectionRenderScale);
        if (profile.IsCircular)
        {
            var round = new PipeVisual3D
            {
                Point1 = ToMedia(start),
                Point2 = ToMedia(end),
                Diameter = Math.Max(0.01, profile.Diameter * scale),
                Fill = brush
            };
            AddSelectableVisual(round, new SelectedModelObject { Type = SelectedModelObjectType.Element, Id = element.Id, Name = $"E{element.Id}" });
            return true;
        }

        var axes = StructuralSolver.GetLocalAxes(start, end, element.RollAngleRadians);
        var mesh = new MeshGeometry3D();
        foreach (var rectangle in profile.Rectangles)
            AddExtrudedRectangle(mesh, start, end, axes, rectangle, scale);

        if (mesh.Positions.Count == 0)
            return false;

        var material = new DiffuseMaterial(brush);
        var model = new GeometryModel3D(mesh, material) { BackMaterial = material };
        var visual = new ModelVisual3D { Content = model };
        AddSelectableVisual(visual, new SelectedModelObject { Type = SelectedModelObjectType.Element, Id = element.Id, Name = $"E{element.Id}" });
        return true;
    }

    private static void AddExtrudedRectangle(
        MeshGeometry3D mesh,
        CorePoint3D start,
        CorePoint3D end,
        LocalAxes axes,
        SectionVisualRectangle rectangle,
        double scale)
    {
        double halfY = rectangle.WidthY * scale / 2.0;
        double halfZ = rectangle.DepthZ * scale / 2.0;
        double centerY = rectangle.CenterY * scale;
        double centerZ = rectangle.CenterZ * scale;
        if (halfY <= 0 || halfZ <= 0)
            return;

        var startCorners = new[]
        {
            SectionPoint(start, axes, centerY - halfY, centerZ - halfZ),
            SectionPoint(start, axes, centerY + halfY, centerZ - halfZ),
            SectionPoint(start, axes, centerY + halfY, centerZ + halfZ),
            SectionPoint(start, axes, centerY - halfY, centerZ + halfZ)
        };
        var endCorners = new[]
        {
            SectionPoint(end, axes, centerY - halfY, centerZ - halfZ),
            SectionPoint(end, axes, centerY + halfY, centerZ - halfZ),
            SectionPoint(end, axes, centerY + halfY, centerZ + halfZ),
            SectionPoint(end, axes, centerY - halfY, centerZ + halfZ)
        };

        int baseIndex = mesh.Positions.Count;
        foreach (var point in startCorners.Concat(endCorners))
            mesh.Positions.Add(ToMedia(point));

        AddQuad(mesh, baseIndex + 0, baseIndex + 1, baseIndex + 2, baseIndex + 3);
        AddQuad(mesh, baseIndex + 4, baseIndex + 7, baseIndex + 6, baseIndex + 5);
        AddQuad(mesh, baseIndex + 0, baseIndex + 4, baseIndex + 5, baseIndex + 1);
        AddQuad(mesh, baseIndex + 1, baseIndex + 5, baseIndex + 6, baseIndex + 2);
        AddQuad(mesh, baseIndex + 2, baseIndex + 6, baseIndex + 7, baseIndex + 3);
        AddQuad(mesh, baseIndex + 3, baseIndex + 7, baseIndex + 4, baseIndex + 0);
    }

    private static CorePoint3D SectionPoint(CorePoint3D origin, LocalAxes axes, double y, double z)
    {
        return Offset(
            origin,
            axes.YAxis.X * y + axes.ZAxis.X * z,
            axes.YAxis.Y * y + axes.ZAxis.Y * z,
            axes.YAxis.Z * y + axes.ZAxis.Z * z);
    }

    private static void AddQuad(MeshGeometry3D mesh, int a, int b, int c, int d)
    {
        mesh.TriangleIndices.Add(a);
        mesh.TriangleIndices.Add(b);
        mesh.TriangleIndices.Add(c);
        mesh.TriangleIndices.Add(a);
        mesh.TriangleIndices.Add(c);
        mesh.TriangleIndices.Add(d);
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

        var definition = GetDiagramDefinition(_options.DiagramMode);
        var (_, span) = GetBounds();
        double maxValue = result.StationResults
            .Select(s => GetDiagramStationValue(s, definition.Mode))
            .Select(Math.Abs)
            .DefaultIfEmpty(0)
            .Max();
        if (maxValue <= 1e-9)
            return;

        double maxOffset = Math.Min(span * 0.08, 0.75) * Math.Clamp(_options.DiagramScale, MinimumDiagramScale, MaximumDiagramScale);
        var axes = StructuralSolver.GetLocalAxes(start, end, element.RollAngleRadians);
        var offsetAxis = GetDiagramOffsetAxis(definition.Mode, axes);
        var stations = result.StationResults
            .OrderBy(s => s.RelativePosition)
            .ToList();
        var points = stations
            .Select(s =>
            {
                var basePoint = Interpolate(start, end, s.RelativePosition);
                double value = GetDiagramStationValue(s, definition.Mode);
                double offset = value / maxValue * maxOffset;
                return Offset(basePoint, offsetAxis.X * offset, offsetAxis.Y * offset, offsetAxis.Z * offset);
            })
            .ToList();

        AddLine(start, end, Colors.Gray, 1);
        for (int i = 0; i < points.Count - 1; i++)
        {
            double segmentValue = (GetDiagramStationValue(stations[i], definition.Mode) + GetDiagramStationValue(stations[i + 1], definition.Mode)) / 2.0;
            AddLine(points[i], points[i + 1], segmentValue >= 0 ? definition.PositiveColor : definition.NegativeColor, 2.5);
        }

        for (int i = 0; i < points.Count; i++)
        {
            double value = GetDiagramStationValue(stations[i], definition.Mode);
            if (Math.Abs(value) > maxValue * 0.02)
            {
                var basePoint = Interpolate(start, end, stations[i].RelativePosition);
                AddLine(basePoint, points[i], value >= 0 ? definition.PositiveColor : definition.NegativeColor, 1);
            }
        }

        if (_options.Layers.Labels)
        {
            var midStation = stations.OrderBy(s => Math.Abs(s.RelativePosition - 0.5)).First();
            var midPoint = points[points.Count / 2];
            AddText(FormatDiagramValue(midStation, definition), midPoint, new SolidColorBrush(definition.PositiveColor));

            if (_options.ShowDiagramExtrema)
                AddDiagramExtremaLabels(stations, points, definition);
        }
    }

    private void AddUtilizationMarker(StructuralElement element, CorePoint3D start, CorePoint3D end)
    {
        double utilization = _result?.DesignChecks.Where(c => c.ElementId == element.Id).Select(c => c.Utilization).DefaultIfEmpty(0).Max() ?? 0;
        if (utilization <= 1e-9 || !_options.Layers.Labels)
            return;

        AddText($"U={utilization:F2}", Offset(Mid(start, end), 0, 0, 0.18), GetUtilizationBrush(element.Id, Math.Max(1e-9, _result?.MaxUtilization ?? 0)));
    }

    private void AddDiagramExtremaLabels(
        IReadOnlyList<ElementStationResult> stations,
        IReadOnlyList<CorePoint3D> points,
        DiagramDefinition definition)
    {
        var indexed = stations
            .Select((station, index) => new { Station = station, Index = index, Value = GetDiagramStationValue(station, definition.Mode) })
            .ToList();

        var positive = indexed.Where(x => x.Value > 1e-9).OrderByDescending(x => x.Value).FirstOrDefault();
        var negative = indexed.Where(x => x.Value < -1e-9).OrderBy(x => x.Value).FirstOrDefault();

        if (positive != null)
            AddText($"max + {FormatDiagramValue(positive.Station, definition)} @ {positive.Station.RelativePosition:F2}", points[positive.Index], new SolidColorBrush(definition.PositiveColor));
        if (negative != null)
            AddText($"max - {FormatDiagramValue(negative.Station, definition)} @ {negative.Station.RelativePosition:F2}", points[negative.Index], new SolidColorBrush(definition.NegativeColor));
    }

    private static double GetDiagramStationValue(ElementStationResult station, ResultDiagramMode mode)
    {
        return NormalizeDiagramMode(mode) switch
        {
            ResultDiagramMode.AxialForce => station.AxialForce,
            ResultDiagramMode.ShearY => station.ShearY,
            ResultDiagramMode.ShearZ => station.ShearZ,
            ResultDiagramMode.Torsion => station.Torsion,
            ResultDiagramMode.MomentY => station.MomentY,
            ResultDiagramMode.MomentZ => station.MomentZ,
            ResultDiagramMode.MomentDiagram => Math.Abs(station.MomentZ) >= Math.Abs(station.MomentY) ? station.MomentZ : station.MomentY,
            _ => Math.Abs(station.ShearY) >= Math.Abs(station.ShearZ) ? station.ShearY : station.ShearZ
        };
    }

    private static string FormatDiagramValue(ElementStationResult station, DiagramDefinition definition)
    {
        double value = GetDiagramStationValue(station, definition.Mode);
        return definition.UsesMomentUnits
            ? $"{definition.Symbol}={FormatSignedMoment(value)}"
            : $"{definition.Symbol}={FormatSignedForce(value)}";
    }

    private static CoreVector3D GetDiagramOffsetAxis(ResultDiagramMode mode, LocalAxes axes)
    {
        return NormalizeDiagramMode(mode) switch
        {
            ResultDiagramMode.ShearY => axes.YAxis,
            ResultDiagramMode.ShearZ => axes.ZAxis,
            ResultDiagramMode.MomentY => axes.ZAxis,
            ResultDiagramMode.MomentZ => new CoreVector3D(-axes.YAxis.X, -axes.YAxis.Y, -axes.YAxis.Z),
            _ => axes.ZAxis
        };
    }

    private static DiagramDefinition GetDiagramDefinition(ResultDiagramMode mode)
    {
        return NormalizeDiagramMode(mode) switch
        {
            ResultDiagramMode.AxialForce => new DiagramDefinition(ResultDiagramMode.AxialForce, "N", Colors.RoyalBlue, Colors.Firebrick, false),
            ResultDiagramMode.ShearY => new DiagramDefinition(ResultDiagramMode.ShearY, "Vy", Colors.DarkOrange, Colors.SaddleBrown, false),
            ResultDiagramMode.ShearZ => new DiagramDefinition(ResultDiagramMode.ShearZ, "Vz", Colors.OrangeRed, Colors.DarkRed, false),
            ResultDiagramMode.Torsion => new DiagramDefinition(ResultDiagramMode.Torsion, "T", Colors.Teal, Colors.DarkViolet, true),
            ResultDiagramMode.MomentY => new DiagramDefinition(ResultDiagramMode.MomentY, "My", Colors.DarkViolet, Colors.MediumVioletRed, true),
            ResultDiagramMode.MomentZ => new DiagramDefinition(ResultDiagramMode.MomentZ, "Mz", Colors.Purple, Colors.Crimson, true),
            ResultDiagramMode.MomentDiagram => new DiagramDefinition(ResultDiagramMode.MomentDiagram, "M", Colors.DarkViolet, Colors.MediumVioletRed, true),
            _ => new DiagramDefinition(ResultDiagramMode.ForceDiagram, "V", Colors.DarkOrange, Colors.SaddleBrown, false)
        };
    }

    private static bool IsResultDiagramMode(ResultDiagramMode mode)
    {
        return mode is ResultDiagramMode.AxialForce
            or ResultDiagramMode.ShearY
            or ResultDiagramMode.ShearZ
            or ResultDiagramMode.Torsion
            or ResultDiagramMode.MomentY
            or ResultDiagramMode.MomentZ
            or ResultDiagramMode.ForceDiagram
            or ResultDiagramMode.MomentDiagram
            or ResultDiagramMode.Utilization;
    }

    private static ResultDiagramMode NormalizeDiagramMode(ResultDiagramMode mode)
    {
        return mode switch
        {
            ResultDiagramMode.ForceDiagram => ResultDiagramMode.ShearY,
            ResultDiagramMode.MomentDiagram => ResultDiagramMode.MomentZ,
            _ => mode
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
        if (_result != null && IsResultDiagramMode(_options.DiagramMode) && _options.DiagramMode != ResultDiagramMode.Utilization)
        {
            var definition = GetDiagramDefinition(_options.DiagramMode);
            AddText(
                $"Diagram: {GetDiagramModeName(_options.DiagramMode)} | +={ColorName(definition.PositiveColor)} | -={ColorName(definition.NegativeColor)} | Scale={_options.DiagramScale:F2}x",
                Offset(p, 0, 0, -span * 0.10),
                Brushes.DimGray);
        }
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
            ResultDiagramMode.AxialForce when result.AxialForce > 1e-6 => Brushes.RoyalBlue,
            ResultDiagramMode.AxialForce when result.AxialForce < -1e-6 => Brushes.Firebrick,
            ResultDiagramMode.ShearY or ResultDiagramMode.ShearZ or ResultDiagramMode.ForceDiagram => Brushes.DarkOrange,
            ResultDiagramMode.Torsion => Brushes.Teal,
            ResultDiagramMode.MomentY or ResultDiagramMode.MomentZ or ResultDiagramMode.MomentDiagram => Brushes.DarkViolet,
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
    private static string FormatSignedForce(double force) => force >= 0 ? $"+{FormatForce(force)}" : FormatForce(force);
    private static string FormatSignedMoment(double moment) => moment >= 0 ? $"+{FormatMoment(moment)}" : FormatMoment(moment);

    private static string GetDiagramModeName(ResultDiagramMode mode)
    {
        return mode switch
        {
            ResultDiagramMode.Rendered => "Rendered",
            ResultDiagramMode.Wireframe => "Wireframe",
            ResultDiagramMode.Deformed => "Deformed",
            ResultDiagramMode.AxialForce => "Axial N",
            ResultDiagramMode.ShearY => "Shear Vy",
            ResultDiagramMode.ShearZ => "Shear Vz",
            ResultDiagramMode.Torsion => "Torsion T",
            ResultDiagramMode.MomentY => "Moment My",
            ResultDiagramMode.MomentZ => "Moment Mz",
            ResultDiagramMode.ForceDiagram => "Force V envelope",
            ResultDiagramMode.MomentDiagram => "Moment envelope",
            ResultDiagramMode.Utilization => "Utilization",
            _ => mode.ToString()
        };
    }

    private static string ColorName(Color color)
    {
        if (color == Colors.RoyalBlue) return "blue";
        if (color == Colors.Firebrick) return "red";
        if (color == Colors.DarkOrange) return "orange";
        if (color == Colors.SaddleBrown) return "brown";
        if (color == Colors.OrangeRed) return "orange-red";
        if (color == Colors.DarkRed) return "dark red";
        if (color == Colors.Teal) return "teal";
        if (color == Colors.DarkViolet) return "violet";
        if (color == Colors.MediumVioletRed) return "magenta";
        if (color == Colors.Purple) return "purple";
        if (color == Colors.Crimson) return "crimson";
        return color.ToString();
    }

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

    private sealed record DiagramDefinition(
        ResultDiagramMode Mode,
        string Symbol,
        Color PositiveColor,
        Color NegativeColor,
        bool UsesMomentUnits);
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
