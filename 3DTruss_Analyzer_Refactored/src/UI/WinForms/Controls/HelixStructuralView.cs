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
    private StructuralModel? _model;
    private StructuralAnalysisResult? _result;
    private TrussSolver? _legacySolver;
    private AnalysisResult? _legacyResult;
    private ViewerDisplayOptions _options = new();
    private int _selectedElementId;
    private int _selectedNodeId;

    public HelixStructuralView()
    {
        Dock = Forms.DockStyle.Fill;
        BuildToolbar();
        _host.Child = _viewport;
        Controls.Add(_host);
        Controls.Add(_toolbar);
        _viewport.Children.Add(new SunLight());
        _viewport.Children.Add(_scene);
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
        _selectedNodeId = selection.Type == SelectedModelObjectType.Node ? selection.Id : 0;
        _selectedElementId = selection.Type == SelectedModelObjectType.Element ? selection.Id : 0;
        RefreshView();
    }

    public void RefreshView()
    {
        _scene.Children.Clear();
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
            _scene.Children.Add(new PipeVisual3D
            {
                Point1 = ToMedia(start.Position),
                Point2 = ToMedia(end.Position),
                Diameter = diameter,
                Fill = brush
            });

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
            _scene.Children.Add(new SphereVisual3D
            {
                Center = ToMedia(node.Position),
                Radius = node.Id == _selectedNodeId ? 0.11 : 0.075,
                Fill = node.IsConstrained ? Brushes.SeaGreen : Brushes.Black
            });
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
            _scene.Children.Add(new BoxVisual3D
            {
                Center = ToMedia(new CorePoint3D(p.X, p.Y, p.Z - 0.08)),
                Width = 0.25,
                Length = 0.25,
                Height = 0.08,
                Fill = Brushes.DarkSlateGray
            });
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
                AddLoadArrow(node.Position, node.AppliedForce, $"F {FormatVectorForce(node.AppliedForce)}");
            if (node.AppliedMoment.Magnitude > 1e-9)
                AddText($"M {FormatVectorMoment(node.AppliedMoment)}", Offset(node.Position, 0, 0, 0.25), Brushes.DarkOrange);
        }

        foreach (var load in _model.Loads)
        {
            switch (load)
            {
                case NodalLoad nodal when nodes.TryGetValue(nodal.NodeId, out var node):
                    if (nodal.Force.Magnitude > 1e-9)
                        AddLoadArrow(node.Position, nodal.Force, $"{nodal.LoadCaseId} {FormatVectorForce(nodal.Force)}");
                    if (nodal.Moment.Magnitude > 1e-9)
                        AddText($"{nodal.LoadCaseId} M {FormatVectorMoment(nodal.Moment)}", Offset(node.Position, 0, 0, 0.35), Brushes.DarkOrange);
                    break;
                case MemberPointLoad point when elements.TryGetValue(point.ElementId, out var element) &&
                    nodes.TryGetValue(element.StartNodeId, out var start) &&
                    nodes.TryGetValue(element.EndNodeId, out var end):
                    var p = Interpolate(start.Position, end.Position, point.RelativeDistance);
                    if (point.Force.Magnitude > 1e-9)
                        AddLoadArrow(p, point.Force, $"{point.LoadCaseId} P {FormatVectorForce(point.Force)}");
                    break;
                case MemberDistributedLoad distributed when elements.TryGetValue(distributed.ElementId, out var element) &&
                    nodes.TryGetValue(element.StartNodeId, out var start) &&
                    nodes.TryGetValue(element.EndNodeId, out var end):
                    for (int i = 1; i <= 3; i++)
                        AddLoadArrow(Interpolate(start.Position, end.Position, i / 4.0), distributed.ForcePerLength, string.Empty, 0.35);
                    AddText($"{distributed.LoadCaseId} w {FormatVectorDistributedForce(distributed.ForcePerLength)}", Offset(Mid(start.Position, end.Position), 0, 0, 0.25), Brushes.DarkOrange);
                    break;
            }
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
        double value = _options.DiagramMode switch
        {
            ResultDiagramMode.MomentDiagram => Math.Max(result.MomentY, result.MomentZ),
            ResultDiagramMode.Utilization => _result?.DesignChecks.Where(c => c.ElementId == element.Id).Select(c => c.Utilization).DefaultIfEmpty(0).Max() ?? 0,
            _ => Math.Max(Math.Abs(result.AxialForce), Math.Max(result.ShearY, result.ShearZ))
        };
        if (value <= 1e-9)
            return;

        var (_, span) = GetBounds();
        double offset = Math.Min(span * 0.06, 0.55);
        var axes = StructuralSolver.GetLocalAxes(start, end, element.RollAngleRadians);
        var p1 = Offset(start, axes.ZAxis.X * offset, axes.ZAxis.Y * offset, axes.ZAxis.Z * offset);
        var p2 = Offset(end, axes.ZAxis.X * offset, axes.ZAxis.Y * offset, axes.ZAxis.Z * offset);
        AddLine(start, p1, Colors.DarkOrange, 2);
        AddLine(p1, p2, Colors.DarkOrange, 2);
        AddLine(p2, end, Colors.DarkOrange, 2);
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
