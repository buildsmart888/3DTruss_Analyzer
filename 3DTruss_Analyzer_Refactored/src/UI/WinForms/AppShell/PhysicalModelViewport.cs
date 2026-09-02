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

    public event EventHandler<Guid>? ObjectSelected;

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
        var (center, span) = GetBounds(_document.Model.Nodes);
        _scene.Children.Add(new GridLinesVisual3D { Center = new(center.X, center.Y, 0), Width = span * 1.8, Length = span * 1.8, MajorDistance = 1, MinorDistance = 1, Thickness = .01, Fill = Brushes.LightSteelBlue });
        AddAxes(span);
        foreach (var line in _document.Model.LineObjects)
        {
            if (!nodes.TryGetValue(line.StartNodeId, out var start) || !nodes.TryGetValue(line.EndNodeId, out var end)) continue;
            var selected = line.Id == _selectedId;
            var pipe = new PipeVisual3D { Point1 = ToMedia(start.Position), Point2 = ToMedia(end.Position), Diameter = selected ? Math.Max(.08, span * .012) : Math.Max(.05, span * .008), Fill = GetLineBrush(line.Id) };
            AddSelectable(pipe, line.Id);
            AddText(line.Label, Mid(start.Position, end.Position), Brushes.SlateGray);
        }
        foreach (var node in _document.Model.Nodes)
        {
            var selected = node.Id == _selectedId;
            var sphere = new SphereVisual3D { Center = ToMedia(node.Position), Radius = selected ? Math.Max(.12, span * .018) : Math.Max(.08, span * .012), Fill = selected ? Brushes.Gold : Brushes.MidnightBlue };
            AddSelectable(sphere, node.Id);
            AddText(node.Label, new(node.Position.X, node.Position.Y, node.Position.Z + Math.Max(.12, span * .018)), selected ? Brushes.DarkGoldenrod : Brushes.Black);
        }
        AddText("Z-UP · physical model · click node/member to select", new(center.X - span * .55, center.Y - span * .55, center.Z + span * .55), Brushes.DimGray);
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
        foreach (var hit in Viewport3DHelper.FindHits(_viewport.Viewport, e.GetPosition(_viewport)))
        {
            if (hit.Visual is not null && _visualIds.TryGetValue(hit.Visual, out var visualId) || hit.Model is not null && _modelIds.TryGetValue(hit.Model, out visualId))
            {
                _selectedId = visualId; Refresh(); ObjectSelected?.Invoke(this, visualId); e.Handled = true; return;
            }
        }
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
