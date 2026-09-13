namespace TrussAnalyzer.UI.AppShell;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using TrussAnalyzer.Core.Domain.V1;
using WpfModel3D = System.Windows.Media.Media3D.Model3D;

/// <summary>Physical-only Helix viewport. It renders Model3D directly and exposes identity-based selection.</summary>
public sealed class PhysicalModelViewport : UserControl
{
    private readonly HelixViewport3D _viewport = new()
    {
        Background = new LinearGradientBrush(Color.FromRgb(234, 240, 243), Colors.White, 90),
        ShowCoordinateSystem = true,
        ShowViewCube = true,
        ZoomExtentsWhenLoaded = true
    };
    private readonly ModelVisual3D _scene = new();
    private readonly Dictionary<Visual3D, Guid> _visualIds = new();
    private readonly Dictionary<WpfModel3D, Guid> _modelIds = new();
    private ProjectDocument? _document;
    private Guid? _selectedId;
    private double _workPlaneZ;
    private Point3DValue _modelCenter;
    private double _modelSpan = 10;
    private Guid? _activeLoadPatternId;
    private bool _showLoads = true;
    private bool _showLoadLabels = true;
    private double _loadScale = .001;

    public event EventHandler<Guid>? ObjectSelected;
    public event EventHandler<Point3DValue>? PointPlaced;
    public PlacementMode PlacementMode { get; set; }
    public bool ShowLabels { get; set; } = true;

    public void SetLoadDisplay(Guid? patternId, bool showLoads, double scale, bool showLabels)
    {
        _activeLoadPatternId = patternId; _showLoads = showLoads; _loadScale = Math.Max(1e-6, scale); _showLoadLabels = showLabels; Refresh();
    }

    public PhysicalModelViewport()
    {
        Content = _viewport;
        _viewport.Children.Add(new SunLight());
        _viewport.Children.Add(_scene);
        _viewport.MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    public void SetDocument(ProjectDocument? document)
    {
        _document = document;
        Refresh();
    }

    public void SelectObject(Guid? objectId)
    {
        _selectedId = objectId;
        Refresh();
    }

    private void Refresh()
    {
        _scene.Children.Clear(); _visualIds.Clear(); _modelIds.Clear();
        if (_document is null || _document.Model.Nodes.Count == 0)
        {
            AddText("Create nodes to begin physical modeling", new(0, 0, 0), Brushes.DimGray);
            return;
        }

        var nodes = _document.Model.Nodes.ToDictionary(node => node.Id);
        var renderLabels = ShowLabels && _document.Model.Nodes.Count <= 2000;
        var (center, span) = GetBounds(_document.Model.Nodes);
        _modelCenter = center; _modelSpan = span; _workPlaneZ = center.Z;
        _scene.Children.Add(new GridLinesVisual3D { Center = new(center.X, center.Y, 0), Width = span * 1.8, Length = span * 1.8, MajorDistance = 1, MinorDistance = 1, Thickness = .01, Fill = Brushes.LightSteelBlue });
        AddAxes(span);
        foreach (var line in _document.Model.LineObjects)
        {
            if (!nodes.TryGetValue(line.StartNodeId, out var start) || !nodes.TryGetValue(line.EndNodeId, out var end)) continue;
            var selected = line.Id == _selectedId;
            var pipe = new PipeVisual3D { Point1 = ToMedia(start.Position), Point2 = ToMedia(end.Position), Diameter = selected ? Math.Max(.08, span * .012) : Math.Max(.05, span * .008), Fill = GetLineBrush(line.Id) };
            AddSelectable(pipe, line.Id);
            if (renderLabels) AddText(line.Label, Mid(start.Position, end.Position), Brushes.SlateGray);
        }
        foreach (var node in _document.Model.Nodes)
        {
            var selected = node.Id == _selectedId;
            var sphere = new SphereVisual3D { Center = ToMedia(node.Position), Radius = selected ? Math.Max(.12, span * .018) : Math.Max(.08, span * .012), Fill = selected ? Brushes.Gold : Brushes.MidnightBlue };
            AddSelectable(sphere, node.Id);
            if (renderLabels) AddText(node.Label, new(node.Position.X, node.Position.Y, node.Position.Z + Math.Max(.12, span * .018)), selected ? Brushes.DarkGoldenrod : Brushes.Black);
        }
        if (_showLoads) RenderLoads(nodes, renderLabels);
        if (renderLabels) AddText("Z-UP · physical model · click node/member to select", new(center.X - span * .55, center.Y - span * .55, center.Z + span * .55), Brushes.DimGray);
    }

    private void RenderLoads(IReadOnlyDictionary<Guid, Node3D> nodes, bool renderLabels)
    {
        var patterns = _document!.LoadDefinitions.LoadPatterns.ToDictionary(pattern => pattern.Id);
        foreach (var assignment in _document.LoadDefinitions.Assignments.Where(item => _activeLoadPatternId is null || item.LoadPatternId == _activeLoadPatternId))
        {
            if (!patterns.ContainsKey(assignment.LoadPatternId)) continue;
            Point3DValue? origin = null; Vector3DValue vector = new(); string label = assignment.Label;
            switch (assignment)
            {
                case NodalLoadAssignment3D nodal when nodes.TryGetValue(nodal.NodeId, out var node): origin = node.Position; vector = nodal.Force; break;
                case LineLoadAssignment3D line when TryLineMidpoint(line.LineObjectId, nodes, out var midpoint): origin = midpoint; vector = line.ForcePerLength; break;
                case LinePointLoadAssignment3D point when TryLinePoint(point.LineObjectId, point.RelativePosition, nodes, out var pointPosition): origin = pointPosition; vector = point.Force; break;
            }
            if (origin is null || vector.Magnitude < 1e-9) continue;
            var scaled = vector.Scale(_loadScale);
            var tip = new Point3DValue(origin.Value.X + scaled.X, origin.Value.Y + scaled.Y, origin.Value.Z + scaled.Z);
            _scene.Children.Add(new ArrowVisual3D { Point1 = ToMedia(origin.Value), Point2 = ToMedia(tip), Diameter = Math.Max(.02, _modelSpan * .002), Fill = Brushes.Orange });
            if (_showLoadLabels && renderLabels) AddText(label, tip, Brushes.DarkOrange);
        }
    }

    private bool TryLineMidpoint(Guid lineId, IReadOnlyDictionary<Guid, Node3D> nodes, out Point3DValue point)
    {
        var line = _document!.Model.LineObjects.FirstOrDefault(item => item.Id == lineId);
        if (line is not null && nodes.TryGetValue(line.StartNodeId, out var a) && nodes.TryGetValue(line.EndNodeId, out var b)) { point = Mid(a.Position, b.Position); return true; }
        point = default; return false;
    }

    private bool TryLinePoint(Guid lineId, double relative, IReadOnlyDictionary<Guid, Node3D> nodes, out Point3DValue point)
    {
        var line = _document!.Model.LineObjects.FirstOrDefault(item => item.Id == lineId);
        if (line is not null && nodes.TryGetValue(line.StartNodeId, out var a) && nodes.TryGetValue(line.EndNodeId, out var b)) { point = new(a.Position.X + (b.Position.X - a.Position.X) * relative, a.Position.Y + (b.Position.Y - a.Position.Y) * relative, a.Position.Z + (b.Position.Z - a.Position.Z) * relative); return true; }
        point = default; return false;
    }

    private void AddAxes(double span)
    {
        var length = Math.Max(1, span * .2);
        _scene.Children.Add(new ArrowVisual3D { Point1 = new(0, 0, 0), Point2 = new(length, 0, 0), Diameter = .035, Fill = Brushes.Red });
        _scene.Children.Add(new ArrowVisual3D { Point1 = new(0, 0, 0), Point2 = new(0, length, 0), Diameter = .035, Fill = Brushes.Green });
        _scene.Children.Add(new ArrowVisual3D { Point1 = new(0, 0, 0), Point2 = new(0, 0, length), Diameter = .035, Fill = Brushes.Blue });
    }

    private Brush GetLineBrush(Guid lineId)
    {
        var color = _document!.Model.Groups.Where(group => group.ObjectIds.Contains(lineId))
            .Select(group => _document.PresentationSettings.GroupDisplayColors.TryGetValue(group.Id, out var value) ? value : null)
            .FirstOrDefault(value => value is not null);
        var brush = color is not null ? (Brush)new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!) : Brushes.SteelBlue;
        brush = brush.Clone(); brush.Opacity = 1 - _document.PresentationSettings.Transparency;
        return brush;
    }

    private void AddSelectable(Visual3D visual, Guid id)
    {
        _scene.Children.Add(visual); _visualIds[visual] = id;
        if (visual is ModelVisual3D modelVisual && modelVisual.Content is not null) _modelIds[modelVisual.Content] = id;
    }

    private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (PlacementMode != PlacementMode.None)
        {
            PointPlaced?.Invoke(this, ScreenToWorkPlane(e.GetPosition(_viewport)));
            e.Handled = true;
            if (PlacementMode == PlacementMode.Node) PlacementMode = PlacementMode.None;
            return;
        }
        foreach (var hit in Viewport3DHelper.FindHits(_viewport.Viewport, e.GetPosition(_viewport)))
        {
            if (hit.Visual is not null && _visualIds.TryGetValue(hit.Visual, out var visualId) || hit.Model is not null && _modelIds.TryGetValue(hit.Model, out visualId))
            {
                _selectedId = visualId; Refresh(); ObjectSelected?.Invoke(this, visualId); e.Handled = true; return;
            }
        }
    }

    public void BeginPlacement(PlacementMode mode, double? planeZ = null)
    {
        PlacementMode = mode;
        if (planeZ is { } z) _workPlaneZ = z;
        Focus();
    }

    private Point3DValue ScreenToWorkPlane(Point point)
    {
        // Authoring plane mapping is intentionally stable and camera-independent: the viewport is a
        // work-plane editor, so a click maps to model bounds on XY and snaps in the application layer.
        var width = Math.Max(1, _viewport.ActualWidth);
        var height = Math.Max(1, _viewport.ActualHeight);
        var x = _modelCenter.X + (point.X / width - .5) * _modelSpan * 1.8;
        var y = _modelCenter.Y + (.5 - point.Y / height) * _modelSpan * 1.8;
        return new Point3DValue(x, y, _workPlaneZ);
    }

    private void AddText(string text, Point3DValue position, Brush brush) => _scene.Children.Add(new BillboardTextVisual3D { Text = text, Position = ToMedia(position), Foreground = brush, Background = Brushes.White });
    private static Point3D ToMedia(Point3DValue point) => new(point.X, point.Y, point.Z);
    private static Point3DValue Mid(Point3DValue a, Point3DValue b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);
    private static (Point3DValue Center, double Span) GetBounds(IReadOnlyCollection<Node3D> nodes)
    {
        var minX = nodes.Min(node => node.Position.X); var maxX = nodes.Max(node => node.Position.X); var minY = nodes.Min(node => node.Position.Y); var maxY = nodes.Max(node => node.Position.Y); var minZ = nodes.Min(node => node.Position.Z); var maxZ = nodes.Max(node => node.Position.Z);
        var span = Math.Max(1, Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ)));
        return (new((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2), span);
    }
}

public enum PlacementMode { None, Node, MemberStart, MemberEnd }
