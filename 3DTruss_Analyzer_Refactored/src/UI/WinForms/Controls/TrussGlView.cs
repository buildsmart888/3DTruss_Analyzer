namespace TrussAnalyzer.UI.WinForms.Controls;

using System.Globalization;
using OpenTK.Graphics.OpenGL;
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;

/// <summary>
/// OpenGL-backed truss viewer for WinForms.
/// </summary>
public class TrussGlView : UserControl
{
    private readonly OpenTK.GLControl.GLControl _glControl;
    private readonly Panel _fallbackPanel;
    private readonly Panel _viewportHost;
    private readonly ToolStrip _toolbar;
    private readonly TableLayoutPanel _layout;
    private TrussSolver? _solver;
    private AnalysisResult? _result;
    private StructuralModel? _structuralModel;
    private StructuralAnalysisResult? _structuralResult;
    private bool _loaded;
    private bool _dragging;
    private Point _lastMouse;
    private float _yaw = -35f;
    private float _pitch = 22f;
    private float _zoom = 1.0f;
    private bool _showDeformed = true;
    private bool _showLabels = true;
    private bool _showLoads = true;
    private bool _forceFallback2D = true;
    private ViewerColorMode _colorMode = ViewerColorMode.Utilization;

    public TrussGlView()
    {
        _glControl = new OpenTK.GLControl.GLControl
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black
        };
        _fallbackPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };
        _viewportHost = new Panel
        {
            Dock = DockStyle.Fill
        };

        _toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        AddToolbarButton("Fit", (_, _) => FitToModel());
        AddToolbarButton("Iso", (_, _) => SetView(-35f, 22f));
        AddToolbarButton("Top", (_, _) => SetView(0f, 0f));
        AddToolbarButton("Front", (_, _) => SetView(0f, 90f));
        AddToolbarButton("Side", (_, _) => SetView(90f, 90f));
        AddToolbarToggle("Labels", true, item => _showLabels = item.Checked);
        AddToolbarToggle("Deformed", true, item => _showDeformed = item.Checked);
        AddToolbarToggle("Loads", true, item => _showLoads = item.Checked);
        AddToolbarToggle("2D Debug", true, item => _forceFallback2D = item.Checked);
        var colorDrop = new ToolStripDropDownButton("Color");
        foreach (ViewerColorMode mode in Enum.GetValues<ViewerColorMode>())
        {
            colorDrop.DropDownItems.Add(mode.ToString(), null, (_, _) =>
            {
                _colorMode = mode;
                InvalidateViewer();
            });
        }
        _toolbar.Items.Add(colorDrop);

        _glControl.Load += (_, _) => InitializeGl();
        _glControl.Paint += (_, e) => Render(e.Graphics);
        _glControl.Resize += (_, _) => InvalidateViewer();
        _glControl.MouseDown += OnMouseDown;
        _glControl.MouseMove += OnMouseMove;
        _glControl.MouseUp += (_, _) => _dragging = false;
        _glControl.MouseWheel += OnMouseWheel;
        _glControl.HandleCreated += (_, _) => InvalidateViewer();
        _glControl.VisibleChanged += (_, _) => InvalidateViewer();
        _fallbackPanel.Paint += (_, e) => DrawFallback2D(e.Graphics);
        _fallbackPanel.Resize += (_, _) => InvalidateViewer();
        _fallbackPanel.MouseDown += OnMouseDown;
        _fallbackPanel.MouseMove += OnMouseMove;
        _fallbackPanel.MouseUp += (_, _) => _dragging = false;
        _fallbackPanel.MouseWheel += OnMouseWheel;

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _layout.Controls.Add(_toolbar, 0, 0);
        _viewportHost.Controls.Add(_glControl);
        _viewportHost.Controls.Add(_fallbackPanel);
        _layout.Controls.Add(_viewportHost, 0, 1);
        Controls.Add(_layout);
        UpdateViewerSurface();
        VisibleChanged += (_, _) => InvalidateViewer();
    }

    public void SetModel(TrussSolver solver, AnalysisResult? result = null)
    {
        _solver = solver;
        _result = result;
        _structuralModel = null;
        _structuralResult = null;
        InvalidateViewer();
    }

    public void SetModel(StructuralModel model, StructuralAnalysisResult? result = null)
    {
        _structuralModel = model;
        _structuralResult = result;
        _solver = null;
        _result = null;
        InvalidateViewer();
    }

    private void InitializeGl()
    {
        if (!_glControl.HasValidContext)
            return;

        _glControl.MakeCurrent();
        GL.ClearColor(Color.White);
        GL.Enable(EnableCap.DepthTest);
        GL.PointSize(7f);
        _loaded = true;
    }

    private void Render(Graphics fallbackGraphics)
    {
        var nodes = _structuralModel?.Nodes ?? (IReadOnlyList<Node>?)(_solver?.GetNodes()) ?? Array.Empty<Node>();
        if (_forceFallback2D || nodes.Count == 0)
        {
            DrawFallback2D(fallbackGraphics);
            return;
        }

        if (!_loaded)
            InitializeGl();

        if (!_loaded || !_glControl.HasValidContext)
        {
            DrawFallback2D(fallbackGraphics);
            return;
        }

        try
        {
            _glControl.MakeCurrent();
            GL.Viewport(0, 0, Math.Max(1, _glControl.Width), Math.Max(1, _glControl.Height));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            SetupCamera(nodes);
            DrawAxes(GetModelSpan(nodes));
            if (_structuralModel != null)
            {
                DrawStructuralElements(_structuralModel.Elements, nodes);
                DrawLocalAxes(nodes);
            }
            else
            {
                DrawElements(_solver?.GetElements() ?? Array.Empty<Element>(), nodes);
            }
            if (_showDeformed)
                DrawDeformedShape(nodes);
            DrawNodes(nodes);
            DrawSupports(nodes);
            if (_showLoads)
                DrawLoads(nodes);

            _glControl.SwapBuffers();
            if (_showLabels)
                DrawLabelsOverlay(nodes);
        }
        catch
        {
            DrawFallback2D(fallbackGraphics);
        }
    }

    private void SetupCamera(IReadOnlyList<Node> nodes)
    {
        var (center, span) = GetBounds(nodes);
        double half = Math.Max(1.0, span / _zoom);
        double aspect = _glControl.Width <= 0 || _glControl.Height <= 0 ? 1.0 : (double)_glControl.Width / _glControl.Height;

        GL.MatrixMode(MatrixMode.Projection);
        GL.LoadIdentity();
        if (aspect >= 1)
            GL.Ortho(-half * aspect, half * aspect, -half, half, -1000, 1000);
        else
            GL.Ortho(-half, half, -half / aspect, half / aspect, -1000, 1000);

        GL.MatrixMode(MatrixMode.Modelview);
        GL.LoadIdentity();
        GL.Rotate(_pitch, 1, 0, 0);
        GL.Rotate(_yaw, 0, 0, 1);
        GL.Translate(-center.X, -center.Y, -center.Z);
    }

    private void DrawAxes(double span)
    {
        double length = Math.Max(1.0, span * 0.35);
        GL.LineWidth(2f);
        GL.Begin(PrimitiveType.Lines);
        GL.Color3(Color.Red);
        GL.Vertex3(0, 0, 0);
        GL.Vertex3(length, 0, 0);
        GL.Color3(Color.Green);
        GL.Vertex3(0, 0, 0);
        GL.Vertex3(0, length, 0);
        GL.Color3(Color.Blue);
        GL.Vertex3(0, 0, 0);
        GL.Vertex3(0, 0, length);
        GL.End();
    }

    private void DrawElements(IReadOnlyList<Element> elements, IReadOnlyList<Node> nodes)
    {
        var nodeMap = nodes.ToDictionary(n => n.Id);
        GL.LineWidth(3f);
        GL.Begin(PrimitiveType.Lines);
        foreach (var element in elements)
        {
            if (!nodeMap.TryGetValue(element.StartNodeId, out var start) ||
                !nodeMap.TryGetValue(element.EndNodeId, out var end))
                continue;

            var displayElement = _result?.Elements.FirstOrDefault(e => e.Id == element.Id) ?? element;
            SetElementColor(displayElement);
            GL.Vertex3(start.Position.X, start.Position.Y, start.Position.Z);
            GL.Vertex3(end.Position.X, end.Position.Y, end.Position.Z);
        }
        GL.End();
    }

    private void DrawStructuralElements(IReadOnlyList<StructuralElement> elements, IReadOnlyList<Node> nodes)
    {
        var nodeMap = nodes.ToDictionary(n => n.Id);
        GL.LineWidth(3f);
        GL.Begin(PrimitiveType.Lines);
        foreach (var element in elements)
        {
            if (!nodeMap.TryGetValue(element.StartNodeId, out var start) ||
                !nodeMap.TryGetValue(element.EndNodeId, out var end))
                continue;

            SetStructuralElementColor(element.Id);
            GL.Vertex3(start.Position.X, start.Position.Y, start.Position.Z);
            GL.Vertex3(end.Position.X, end.Position.Y, end.Position.Z);
        }
        GL.End();
    }

    private void DrawDeformedShape(IReadOnlyList<Node> nodes)
    {
        if (_structuralModel == null || _structuralResult == null || _structuralResult.NodeResults.Count == 0)
            return;

        double span = GetModelSpan(nodes);
        double max = Math.Max(1e-12, _structuralResult.NodeResults.Max(n => n.Displacement.Magnitude));
        double scale = span * 0.12 / max;
        var nodeMap = nodes.ToDictionary(n => n.Id);
        var resultMap = _structuralResult.NodeResults.ToDictionary(n => n.NodeId);

        GL.LineWidth(1.5f);
        GL.Begin(PrimitiveType.Lines);
        GL.Color3(Color.DarkViolet);
        foreach (var element in _structuralModel.Elements)
        {
            if (!nodeMap.TryGetValue(element.StartNodeId, out var start) ||
                !nodeMap.TryGetValue(element.EndNodeId, out var end) ||
                !resultMap.TryGetValue(element.StartNodeId, out var rs) ||
                !resultMap.TryGetValue(element.EndNodeId, out var re))
                continue;

            GL.Vertex3(start.Position.X + rs.Displacement.X * scale, start.Position.Y + rs.Displacement.Y * scale, start.Position.Z + rs.Displacement.Z * scale);
            GL.Vertex3(end.Position.X + re.Displacement.X * scale, end.Position.Y + re.Displacement.Y * scale, end.Position.Z + re.Displacement.Z * scale);
        }
        GL.End();
    }

    private void DrawNodes(IReadOnlyList<Node> nodes)
    {
        GL.PointSize(8f);
        GL.Begin(PrimitiveType.Points);
        foreach (var node in nodes)
        {
            GL.Color3(node.IsConstrained ? Color.DarkGreen : Color.Black);
            GL.Vertex3(node.Position.X, node.Position.Y, node.Position.Z);
        }
        GL.End();
    }

    private void DrawLoads(IReadOnlyList<Node> nodes)
    {
        double span = GetModelSpan(nodes);
        double scale = span * 0.18;

        GL.LineWidth(2f);
        GL.Begin(PrimitiveType.Lines);
        GL.Color3(Color.DarkOrange);
        foreach (var node in nodes.Where(n => n.AppliedForce.Magnitude > 1e-9))
        {
            var f = node.AppliedForce.Scale(scale / node.AppliedForce.Magnitude);
            GL.Vertex3(node.Position.X, node.Position.Y, node.Position.Z);
            GL.Vertex3(node.Position.X + f.X, node.Position.Y + f.Y, node.Position.Z + f.Z);
        }
        GL.End();

        if (_structuralModel == null)
            return;

        var nodeMap = nodes.ToDictionary(n => n.Id);
        var elementMap = _structuralModel.Elements.ToDictionary(e => e.Id);
        GL.LineWidth(2f);
        GL.Begin(PrimitiveType.Lines);
        GL.Color3(Color.DarkOrange);
        foreach (var load in _structuralModel.Loads)
        {
            switch (load)
            {
                case NodalLoad nodal when nodeMap.TryGetValue(nodal.NodeId, out var node) && nodal.Force.Magnitude > 1e-9:
                {
                    var f = nodal.Force.Scale(scale / nodal.Force.Magnitude);
                    GL.Vertex3(node.Position.X, node.Position.Y, node.Position.Z);
                    GL.Vertex3(node.Position.X + f.X, node.Position.Y + f.Y, node.Position.Z + f.Z);
                    break;
                }
                case MemberDistributedLoad distributed when elementMap.TryGetValue(distributed.ElementId, out var element) &&
                    nodeMap.TryGetValue(element.StartNodeId, out var start) &&
                    nodeMap.TryGetValue(element.EndNodeId, out var end) &&
                    distributed.ForcePerLength.Magnitude > 1e-9:
                {
                    var mid = new Point3D((start.Position.X + end.Position.X) / 2, (start.Position.Y + end.Position.Y) / 2, (start.Position.Z + end.Position.Z) / 2);
                    var f = distributed.ForcePerLength.Scale(scale / distributed.ForcePerLength.Magnitude);
                    GL.Vertex3(mid.X, mid.Y, mid.Z);
                    GL.Vertex3(mid.X + f.X, mid.Y + f.Y, mid.Z + f.Z);
                    break;
                }
            }
        }
        GL.End();
    }

    private void DrawSupports(IReadOnlyList<Node> nodes)
    {
        double span = GetModelSpan(nodes);
        double size = span * 0.035;
        GL.LineWidth(2f);
        GL.Begin(PrimitiveType.Lines);
        GL.Color3(Color.SeaGreen);
        foreach (var node in nodes.Where(n => n.IsConstrained))
        {
            GL.Vertex3(node.Position.X - size, node.Position.Y - size, node.Position.Z);
            GL.Vertex3(node.Position.X + size, node.Position.Y - size, node.Position.Z);
            GL.Vertex3(node.Position.X + size, node.Position.Y - size, node.Position.Z);
            GL.Vertex3(node.Position.X, node.Position.Y + size, node.Position.Z);
            GL.Vertex3(node.Position.X, node.Position.Y + size, node.Position.Z);
            GL.Vertex3(node.Position.X - size, node.Position.Y - size, node.Position.Z);
        }
        GL.End();
    }

    private void DrawLocalAxes(IReadOnlyList<Node> nodes)
    {
        if (_structuralModel == null)
            return;

        var nodeMap = nodes.ToDictionary(n => n.Id);
        double span = GetModelSpan(nodes);
        double scale = span * 0.06;
        GL.LineWidth(1f);
        foreach (var element in _structuralModel.Elements.OfType<FrameElement3D>())
        {
            if (!nodeMap.TryGetValue(element.StartNodeId, out var start) || !nodeMap.TryGetValue(element.EndNodeId, out var end))
                continue;

            var axes = StructuralSolver.GetLocalAxes(start.Position, end.Position, element.RollAngleRadians);
            var mid = new Point3D((start.Position.X + end.Position.X) / 2, (start.Position.Y + end.Position.Y) / 2, (start.Position.Z + end.Position.Z) / 2);
            GL.Begin(PrimitiveType.Lines);
            GL.Color3(Color.Red);
            GL.Vertex3(mid.X, mid.Y, mid.Z);
            GL.Vertex3(mid.X + axes.XAxis.X * scale, mid.Y + axes.XAxis.Y * scale, mid.Z + axes.XAxis.Z * scale);
            GL.Color3(Color.Green);
            GL.Vertex3(mid.X, mid.Y, mid.Z);
            GL.Vertex3(mid.X + axes.YAxis.X * scale, mid.Y + axes.YAxis.Y * scale, mid.Z + axes.YAxis.Z * scale);
            GL.Color3(Color.Blue);
            GL.Vertex3(mid.X, mid.Y, mid.Z);
            GL.Vertex3(mid.X + axes.ZAxis.X * scale, mid.Y + axes.ZAxis.Y * scale, mid.Z + axes.ZAxis.Z * scale);
            GL.End();
        }
    }

    private static void SetElementColor(Element element)
    {
        if (element.AxialForce > 1e-6)
            GL.Color3(Color.RoyalBlue);
        else if (element.AxialForce < -1e-6)
            GL.Color3(Color.Firebrick);
        else
            GL.Color3(Color.DimGray);
    }

    private void SetStructuralElementColor(int elementId)
    {
        if (_structuralModel != null && _colorMode == ViewerColorMode.Material)
        {
            var element = _structuralModel.Elements.FirstOrDefault(e => e.Id == elementId);
            int materialId = element?.MaterialId ?? 0;
            var colors = new[] { Color.DimGray, Color.RoyalBlue, Color.ForestGreen, Color.DarkOrange, Color.MediumVioletRed, Color.Teal };
            GL.Color3(colors[Math.Abs(materialId) % colors.Length]);
            return;
        }

        if (_structuralModel != null && _colorMode == ViewerColorMode.Section)
        {
            var element = _structuralModel.Elements.FirstOrDefault(e => e.Id == elementId);
            int sectionId = element?.SectionId ?? 0;
            var colors = new[] { Color.DimGray, Color.SlateBlue, Color.OliveDrab, Color.Chocolate, Color.Crimson, Color.DarkCyan };
            GL.Color3(colors[Math.Abs(sectionId) % colors.Length]);
            return;
        }

        var utilization = _structuralResult?.DesignChecks
            .Where(c => c.ElementId == elementId)
            .Select(c => c.Utilization)
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        var axial = _structuralResult?.ElementResults.FirstOrDefault(e => e.ElementId == elementId)?.AxialForce ?? 0;
        var moment = _structuralResult?.ElementResults.FirstOrDefault(e => e.ElementId == elementId) is { } r
            ? Math.Max(r.MomentY, r.MomentZ)
            : 0;

        if (_colorMode == ViewerColorMode.Moment)
        {
            GL.Color3(moment > 1e-6 ? Color.DarkViolet : Color.DimGray);
            return;
        }

        if (_colorMode == ViewerColorMode.AxialForce)
        {
            if (axial > 1e-6)
                GL.Color3(Color.RoyalBlue);
            else if (axial < -1e-6)
                GL.Color3(Color.MediumVioletRed);
            else
                GL.Color3(Color.DimGray);
            return;
        }

        if (utilization > 1.0)
            GL.Color3(Color.Firebrick);
        else if (utilization > 0.75)
            GL.Color3(Color.DarkOrange);
        else if (axial > 1e-6)
            GL.Color3(Color.RoyalBlue);
        else if (axial < -1e-6)
            GL.Color3(Color.MediumVioletRed);
        else
            GL.Color3(Color.DimGray);
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        _dragging = true;
        _lastMouse = e.Location;
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;

        int dx = e.X - _lastMouse.X;
        int dy = e.Y - _lastMouse.Y;
        _yaw += dx * 0.6f;
        _pitch += dy * 0.6f;
        _pitch = Math.Clamp(_pitch, -89f, 89f);
        _lastMouse = e.Location;
        InvalidateViewer();
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        _zoom *= e.Delta > 0 ? 1.12f : 0.89f;
        _zoom = Math.Clamp(_zoom, 0.2f, 8f);
        InvalidateViewer();
    }

    private void DrawLabelsOverlay(IReadOnlyList<Node> nodes)
    {
        using var graphics = _glControl.CreateGraphics();
        using var brush = new SolidBrush(Color.Black);
        foreach (var node in nodes)
        {
            var point = ProjectToScreen(node.Position, nodes);
            graphics.DrawString($"N{node.Id}", Font, brush, point.X + 4, point.Y + 4);
        }

        if (_structuralModel == null)
            return;

        var nodeMap = nodes.ToDictionary(n => n.Id);
        foreach (var element in _structuralModel.Elements)
        {
            if (!nodeMap.TryGetValue(element.StartNodeId, out var start) || !nodeMap.TryGetValue(element.EndNodeId, out var end))
                continue;

            var mid = new Point3D((start.Position.X + end.Position.X) / 2, (start.Position.Y + end.Position.Y) / 2, (start.Position.Z + end.Position.Z) / 2);
            var point = ProjectToScreen(mid, nodes);
            graphics.DrawString($"E{element.Id}", Font, Brushes.DarkSlateGray, point.X + 4, point.Y + 4);
        }
    }

    private void DrawFallback2D(Graphics graphics)
    {
        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var nodes = _structuralModel?.Nodes ?? (IReadOnlyList<Node>?)(_solver?.GetNodes()) ?? Array.Empty<Node>();
        if (nodes.Count == 0)
        {
            graphics.DrawString("No model is loaded.", Font, Brushes.DimGray, 16, 16);
            return;
        }

        var nodeMap = nodes.ToDictionary(n => n.Id);
        using var memberPen = new Pen(Color.DimGray, 2.5f);
        using var tensionPen = new Pen(Color.RoyalBlue, 3f);
        using var compressionPen = new Pen(Color.Firebrick, 3f);
        using var supportPen = new Pen(Color.SeaGreen, 2f);
        using var loadPen = new Pen(Color.DarkOrange, 2f)
        {
            EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor
        };

        if (_structuralModel != null)
        {
            foreach (var element in _structuralModel.Elements)
            {
                if (!nodeMap.TryGetValue(element.StartNodeId, out var start) || !nodeMap.TryGetValue(element.EndNodeId, out var end))
                    continue;

                var result = _structuralResult?.ElementResults.FirstOrDefault(r => r.ElementId == element.Id);
                var pen = result?.AxialForce > 1e-6 ? tensionPen : result?.AxialForce < -1e-6 ? compressionPen : memberPen;
                graphics.DrawLine(pen, ProjectFallback(start.Position, nodes), ProjectFallback(end.Position, nodes));
            }

            DrawFallbackResultDiagrams(graphics, nodes, nodeMap);
            if (_showLabels)
                DrawFallbackElementResultLabels(graphics, nodes, nodeMap);
        }
        else if (_solver != null)
        {
            foreach (var element in _solver.GetElements())
            {
                if (!nodeMap.TryGetValue(element.StartNodeId, out var start) || !nodeMap.TryGetValue(element.EndNodeId, out var end))
                    continue;

                var pen = element.AxialForce > 1e-6 ? tensionPen : element.AxialForce < -1e-6 ? compressionPen : memberPen;
                graphics.DrawLine(pen, ProjectFallback(start.Position, nodes), ProjectFallback(end.Position, nodes));
            }

            if (_showLabels)
                DrawFallbackLegacyElementLabels(graphics, nodes, nodeMap);
        }

        if (_showDeformed && _structuralModel != null && _structuralResult != null && _structuralResult.NodeResults.Count > 0)
        {
            var resultMap = _structuralResult.NodeResults.ToDictionary(n => n.NodeId);
            double span = GetModelSpan(nodes);
            double max = Math.Max(1e-12, _structuralResult.NodeResults.Max(n => n.Displacement.Magnitude));
            double scale = span * 0.12 / max;
            using var deformedPen = new Pen(Color.DarkViolet, 1.5f);
            foreach (var element in _structuralModel.Elements)
            {
                if (!nodeMap.TryGetValue(element.StartNodeId, out var start) ||
                    !nodeMap.TryGetValue(element.EndNodeId, out var end) ||
                    !resultMap.TryGetValue(element.StartNodeId, out var rs) ||
                    !resultMap.TryGetValue(element.EndNodeId, out var re))
                    continue;

                var ps = new Point3D(start.Position.X + rs.Displacement.X * scale, start.Position.Y + rs.Displacement.Y * scale, start.Position.Z + rs.Displacement.Z * scale);
                var pe = new Point3D(end.Position.X + re.Displacement.X * scale, end.Position.Y + re.Displacement.Y * scale, end.Position.Z + re.Displacement.Z * scale);
                graphics.DrawLine(deformedPen, ProjectFallback(ps, nodes), ProjectFallback(pe, nodes));
            }
        }

        foreach (var node in nodes)
        {
            var p = ProjectFallback(node.Position, nodes);
            Brush brush = node.IsConstrained ? Brushes.SeaGreen : Brushes.Black;
            graphics.FillEllipse(brush, p.X - 4, p.Y - 4, 8, 8);
            if (node.IsConstrained)
            {
                graphics.DrawLine(supportPen, p.X - 10, p.Y + 12, p.X + 10, p.Y + 12);
                graphics.DrawLine(supportPen, p.X - 10, p.Y + 12, p.X, p.Y + 2);
                graphics.DrawLine(supportPen, p.X + 10, p.Y + 12, p.X, p.Y + 2);
            }
            if (_showLabels)
                graphics.DrawString($"N{node.Id}", Font, Brushes.Black, p.X + 5, p.Y + 5);
        }

        if (_showLoads)
        {
            DrawFallbackLoads(graphics, nodes, nodeMap, loadPen);
        }

        DrawFallbackLegend(graphics);
    }

    private void DrawFallbackLoads(Graphics graphics, IReadOnlyList<Node> nodes, Dictionary<int, Node> nodeMap, Pen loadPen)
    {
        using var backBrush = new SolidBrush(Color.FromArgb(235, Color.White));
        using var textBrush = new SolidBrush(Color.DarkOrange);

        foreach (var node in nodes)
        {
            if (node.AppliedForce.Magnitude > 1e-9)
                DrawFallbackLoadArrow(graphics, nodes, node.Position, node.AppliedForce, $"F={FormatVectorForce(node.AppliedForce)}", loadPen, backBrush, textBrush);

            if (node.AppliedMoment.Magnitude > 1e-9)
            {
                var p = ProjectFallback(node.Position, nodes);
                DrawFallbackText(graphics, $"M={FormatVectorMoment(node.AppliedMoment)}", new PointF(p.X + 10, p.Y - 26), backBrush, textBrush);
                graphics.DrawArc(loadPen, p.X - 13, p.Y - 13, 26, 26, 25, 285);
            }
        }

        if (_structuralModel == null)
            return;

        foreach (var load in _structuralModel.Loads)
        {
            switch (load)
            {
                case NodalLoad nodal when nodeMap.TryGetValue(nodal.NodeId, out var node):
                    if (nodal.Force.Magnitude > 1e-9)
                        DrawFallbackLoadArrow(graphics, nodes, node.Position, nodal.Force, $"{nodal.LoadCaseId}: F={FormatVectorForce(nodal.Force)}", loadPen, backBrush, textBrush);
                    if (nodal.Moment.Magnitude > 1e-9)
                    {
                        var p = ProjectFallback(node.Position, nodes);
                        DrawFallbackText(graphics, $"{nodal.LoadCaseId}: M={FormatVectorMoment(nodal.Moment)}", new PointF(p.X + 10, p.Y - 44), backBrush, textBrush);
                        graphics.DrawArc(loadPen, p.X - 17, p.Y - 17, 34, 34, 25, 285);
                    }
                    break;

                case MemberPointLoad point:
                    DrawFallbackMemberPointLoad(graphics, nodes, nodeMap, point, loadPen, backBrush, textBrush);
                    break;

                case MemberDistributedLoad distributed:
                    DrawFallbackDistributedLoad(graphics, nodes, nodeMap, distributed, loadPen, backBrush, textBrush);
                    break;
            }
        }
    }

    private void DrawFallbackMemberPointLoad(
        Graphics graphics,
        IReadOnlyList<Node> nodes,
        Dictionary<int, Node> nodeMap,
        MemberPointLoad load,
        Pen loadPen,
        Brush backBrush,
        Brush textBrush)
    {
        var element = _structuralModel?.Elements.FirstOrDefault(e => e.Id == load.ElementId);
        if (element == null ||
            !nodeMap.TryGetValue(element.StartNodeId, out var start) ||
            !nodeMap.TryGetValue(element.EndNodeId, out var end))
            return;

        double a = Math.Clamp(load.RelativeDistance, 0, 1);
        var position = new Point3D(
            start.Position.X + (end.Position.X - start.Position.X) * a,
            start.Position.Y + (end.Position.Y - start.Position.Y) * a,
            start.Position.Z + (end.Position.Z - start.Position.Z) * a);

        if (load.Force.Magnitude > 1e-9)
            DrawFallbackLoadArrow(graphics, nodes, position, load.Force, $"{load.LoadCaseId}: P={FormatVectorForce(load.Force)}", loadPen, backBrush, textBrush);
        if (load.Moment.Magnitude > 1e-9)
        {
            var p = ProjectFallback(position, nodes);
            DrawFallbackText(graphics, $"{load.LoadCaseId}: M={FormatVectorMoment(load.Moment)}", new PointF(p.X + 10, p.Y - 24), backBrush, textBrush);
            graphics.DrawArc(loadPen, p.X - 13, p.Y - 13, 26, 26, 25, 285);
        }
    }

    private void DrawFallbackDistributedLoad(
        Graphics graphics,
        IReadOnlyList<Node> nodes,
        Dictionary<int, Node> nodeMap,
        MemberDistributedLoad load,
        Pen loadPen,
        Brush backBrush,
        Brush textBrush)
    {
        var element = _structuralModel?.Elements.FirstOrDefault(e => e.Id == load.ElementId);
        if (element == null ||
            !nodeMap.TryGetValue(element.StartNodeId, out var start) ||
            !nodeMap.TryGetValue(element.EndNodeId, out var end) ||
            load.ForcePerLength.Magnitude <= 1e-9)
            return;

        for (int i = 1; i <= 3; i++)
        {
            double t = i / 4.0;
            var position = new Point3D(
                start.Position.X + (end.Position.X - start.Position.X) * t,
                start.Position.Y + (end.Position.Y - start.Position.Y) * t,
                start.Position.Z + (end.Position.Z - start.Position.Z) * t);
            DrawFallbackLoadArrow(graphics, nodes, position, load.ForcePerLength, string.Empty, loadPen, backBrush, textBrush, 26);
        }

        var mid = new Point3D(
            (start.Position.X + end.Position.X) / 2,
            (start.Position.Y + end.Position.Y) / 2,
            (start.Position.Z + end.Position.Z) / 2);
        var labelPoint = ProjectFallback(mid, nodes);
        DrawFallbackText(graphics, $"{load.LoadCaseId}: w={FormatVectorDistributedForce(load.ForcePerLength)}", new PointF(labelPoint.X + 12, labelPoint.Y + 12), backBrush, textBrush);
    }

    private void DrawFallbackLoadArrow(
        Graphics graphics,
        IReadOnlyList<Node> nodes,
        Point3D position,
        Vector3D force,
        string label,
        Pen loadPen,
        Brush backBrush,
        Brush textBrush,
        double arrowLength = 42)
    {
        if (force.Magnitude <= 1e-9)
            return;

        var p = ProjectFallback(position, nodes);
        var direction = force.Normalize().Scale(arrowLength);
        var end = new PointF(p.X + (float)direction.X, p.Y - (float)direction.Z);
        graphics.DrawLine(loadPen, p, end);
        if (!string.IsNullOrWhiteSpace(label))
            DrawFallbackText(graphics, label, new PointF(end.X + 5, end.Y - 16), backBrush, textBrush);
    }

    private void DrawFallbackText(Graphics graphics, string text, PointF point, Brush backBrush, Brush textBrush)
    {
        var size = graphics.MeasureString(text, Font);
        graphics.FillRectangle(backBrush, point.X - 2, point.Y - 1, size.Width + 4, size.Height + 2);
        graphics.DrawString(text, Font, textBrush, point);
    }

    private void DrawFallbackLegend(Graphics graphics)
    {
        graphics.DrawString("Fallback 2D viewer active", Font, Brushes.DimGray, 12, 12);
        graphics.DrawString($"Mode: {_colorMode} | Blue=tension, Red=compression, Purple=deformed shape, Orange=result diagram", Font, Brushes.DimGray, 12, 30);
        graphics.DrawString("Truss elements show axial force only; shear and bending moments require FrameElement3D.", Font, Brushes.DimGray, 12, 48);
    }

    private void DrawFallbackResultDiagrams(Graphics graphics, IReadOnlyList<Node> nodes, Dictionary<int, Node> nodeMap)
    {
        if (_structuralModel == null || _structuralResult == null || _structuralResult.ElementResults.Count == 0)
            return;

        var values = _structuralModel.Elements
            .Select(e => Math.Abs(GetFallbackDiagramValue(e.Id)))
            .Where(v => v > 1e-9)
            .ToList();
        if (values.Count == 0)
            return;

        double max = values.Max();
        double amplitude = Math.Max(14, Math.Min(_fallbackPanel.Width, _fallbackPanel.Height) * 0.045);
        using var diagramPen = new Pen(Color.DarkOrange, 1.8f);
        using var fillBrush = new SolidBrush(Color.FromArgb(42, Color.DarkOrange));

        foreach (var element in _structuralModel.Elements)
        {
            if (!nodeMap.TryGetValue(element.StartNodeId, out var start) || !nodeMap.TryGetValue(element.EndNodeId, out var end))
                continue;

            double value = GetFallbackDiagramValue(element.Id);
            if (Math.Abs(value) < 1e-9)
                continue;

            var p1 = ProjectFallback(start.Position, nodes);
            var p2 = ProjectFallback(end.Position, nodes);
            var offset = GetScreenNormal(p1, p2, (float)(Math.Sign(value) * amplitude * Math.Abs(value) / max));
            var q1 = new PointF(p1.X + offset.X, p1.Y + offset.Y);
            var q2 = new PointF(p2.X + offset.X, p2.Y + offset.Y);
            var polygon = new[] { p1, p2, q2, q1 };
            graphics.FillPolygon(fillBrush, polygon);
            graphics.DrawLines(diagramPen, new[] { p1, q1, q2, p2 });
        }
    }

    private void DrawFallbackElementResultLabels(Graphics graphics, IReadOnlyList<Node> nodes, Dictionary<int, Node> nodeMap)
    {
        if (_structuralModel == null)
            return;

        using var backBrush = new SolidBrush(Color.FromArgb(230, Color.White));
        using var textBrush = new SolidBrush(Color.Black);
        foreach (var element in _structuralModel.Elements)
        {
            if (!nodeMap.TryGetValue(element.StartNodeId, out var start) || !nodeMap.TryGetValue(element.EndNodeId, out var end))
                continue;

            var p1 = ProjectFallback(start.Position, nodes);
            var p2 = ProjectFallback(end.Position, nodes);
            var mid = new PointF((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            var normal = GetScreenNormal(p1, p2, -18);
            var labelPoint = new PointF(mid.X + normal.X, mid.Y + normal.Y);
            string label = GetFallbackElementLabel(element.Id, element.Type);
            var size = graphics.MeasureString(label, Font);
            graphics.FillRectangle(backBrush, labelPoint.X - 2, labelPoint.Y - 1, size.Width + 4, size.Height + 2);
            graphics.DrawString(label, Font, textBrush, labelPoint);
        }
    }

    private void DrawFallbackLegacyElementLabels(Graphics graphics, IReadOnlyList<Node> nodes, Dictionary<int, Node> nodeMap)
    {
        if (_solver == null)
            return;

        using var backBrush = new SolidBrush(Color.FromArgb(230, Color.White));
        foreach (var element in _solver.GetElements())
        {
            if (!nodeMap.TryGetValue(element.StartNodeId, out var start) || !nodeMap.TryGetValue(element.EndNodeId, out var end))
                continue;

            var p1 = ProjectFallback(start.Position, nodes);
            var p2 = ProjectFallback(end.Position, nodes);
            var mid = new PointF((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            string label = $"E{element.Id}  N={FormatForce(element.AxialForce)}  S={element.Stress / 1e6:F2} MPa";
            var size = graphics.MeasureString(label, Font);
            graphics.FillRectangle(backBrush, mid.X - 2, mid.Y - 1, size.Width + 4, size.Height + 2);
            graphics.DrawString(label, Font, Brushes.Black, mid);
        }
    }

    private string GetFallbackElementLabel(int elementId, ElementType elementType)
    {
        var result = _structuralResult?.ElementResults.FirstOrDefault(r => r.ElementId == elementId);
        var maxUtilization = _structuralResult?.DesignChecks
            .Where(c => c.ElementId == elementId)
            .Select(c => c.Utilization)
            .DefaultIfEmpty(0)
            .Max() ?? 0;

        if (result == null)
            return $"E{elementId}";

        return _colorMode switch
        {
            ViewerColorMode.AxialForce => $"E{elementId}  N={FormatForce(result.AxialForce)}  S={result.Stress / 1e6:F2} MPa",
            ViewerColorMode.Moment => elementType == ElementType.Truss
                ? $"E{elementId}  M=N/A (truss)"
                : $"E{elementId}  My={FormatMoment(result.MomentY)}  Mz={FormatMoment(result.MomentZ)}",
            ViewerColorMode.Utilization => $"E{elementId}  Util={maxUtilization:F3}",
            ViewerColorMode.Material => $"E{elementId}  Mat",
            ViewerColorMode.Section => $"E{elementId}  Sec",
            _ => $"E{elementId}"
        };
    }

    private double GetFallbackDiagramValue(int elementId)
    {
        var result = _structuralResult?.ElementResults.FirstOrDefault(r => r.ElementId == elementId);
        if (result == null)
            return 0;

        return _colorMode switch
        {
            ViewerColorMode.Moment => Math.MaxMagnitude(result.MomentY, result.MomentZ),
            ViewerColorMode.Utilization => _structuralResult?.DesignChecks
                .Where(c => c.ElementId == elementId)
                .Select(c => c.Utilization)
                .DefaultIfEmpty(0)
                .Max() ?? 0,
            ViewerColorMode.AxialForce => result.AxialForce,
            _ => result.AxialForce
        };
    }

    private static PointF GetScreenNormal(PointF start, PointF end, float length)
    {
        float dx = end.X - start.X;
        float dy = end.Y - start.Y;
        float magnitude = MathF.Max(1e-6f, MathF.Sqrt(dx * dx + dy * dy));
        return new PointF(-dy / magnitude * length, dx / magnitude * length);
    }

    private static string FormatForce(double force)
    {
        double abs = Math.Abs(force);
        return abs >= 1000 ? $"{force / 1000:F2} kN" : $"{force:F1} N";
    }

    private static string FormatMoment(double moment)
    {
        double abs = Math.Abs(moment);
        return abs >= 1000 ? $"{moment / 1000:F2} kN-m" : $"{moment:F1} N-m";
    }

    private static string FormatDistributedForce(double forcePerLength)
    {
        double abs = Math.Abs(forcePerLength);
        return abs >= 1000 ? $"{forcePerLength / 1000:F2} kN/m" : $"{forcePerLength:F1} N/m";
    }

    private static string FormatVectorForce(Vector3D force)
    {
        var parts = new List<string>();
        if (Math.Abs(force.X) > 1e-9) parts.Add($"Fx {FormatForce(force.X)}");
        if (Math.Abs(force.Y) > 1e-9) parts.Add($"Fy {FormatForce(force.Y)}");
        if (Math.Abs(force.Z) > 1e-9) parts.Add($"Fz {FormatForce(force.Z)}");
        return parts.Count == 0 ? "0 N" : string.Join(", ", parts);
    }

    private static string FormatVectorDistributedForce(Vector3D forcePerLength)
    {
        var parts = new List<string>();
        if (Math.Abs(forcePerLength.X) > 1e-9) parts.Add($"wx {FormatDistributedForce(forcePerLength.X)}");
        if (Math.Abs(forcePerLength.Y) > 1e-9) parts.Add($"wy {FormatDistributedForce(forcePerLength.Y)}");
        if (Math.Abs(forcePerLength.Z) > 1e-9) parts.Add($"wz {FormatDistributedForce(forcePerLength.Z)}");
        return parts.Count == 0 ? "0 N/m" : string.Join(", ", parts);
    }

    private static string FormatVectorMoment(Vector3D moment)
    {
        var parts = new List<string>();
        if (Math.Abs(moment.X) > 1e-9) parts.Add($"Mx {FormatMoment(moment.X)}");
        if (Math.Abs(moment.Y) > 1e-9) parts.Add($"My {FormatMoment(moment.Y)}");
        if (Math.Abs(moment.Z) > 1e-9) parts.Add($"Mz {FormatMoment(moment.Z)}");
        return parts.Count == 0 ? "0 N-m" : string.Join(", ", parts);
    }

    private PointF ProjectFallback(Point3D point, IReadOnlyList<Node> nodes)
    {
        var surface = _fallbackPanel.Visible ? (Control)_fallbackPanel : _glControl;
        var (center, span) = GetBounds(nodes);
        double scale = Math.Min(Math.Max(100, surface.Width - 80), Math.Max(100, surface.Height - 80)) / Math.Max(1e-9, span * 1.6);
        double x = point.X - center.X;
        double y = point.Y - center.Y;
        double z = point.Z - center.Z;
        double yaw = _yaw * Math.PI / 180.0;
        double pitch = _pitch * Math.PI / 180.0;
        double xr = x * Math.Cos(yaw) - y * Math.Sin(yaw);
        double yr = x * Math.Sin(yaw) + y * Math.Cos(yaw);
        double zr = z;
        double yp = yr * Math.Cos(pitch) - zr * Math.Sin(pitch);
        return new PointF(
            (float)(surface.Width / 2.0 + xr * scale),
            (float)(surface.Height / 2.0 - yp * scale));
    }

    private PointF ProjectToScreen(Point3D point, IReadOnlyList<Node> nodes)
    {
        var (center, span) = GetBounds(nodes);
        double half = Math.Max(1.0, span / _zoom);
        double aspect = _glControl.Width <= 0 || _glControl.Height <= 0 ? 1.0 : (double)_glControl.Width / _glControl.Height;
        double x = point.X - center.X;
        double y = point.Y - center.Y;
        double z = point.Z - center.Z;
        double yaw = _yaw * Math.PI / 180.0;
        double pitch = _pitch * Math.PI / 180.0;
        double x1 = x * Math.Cos(yaw) - y * Math.Sin(yaw);
        double y1 = x * Math.Sin(yaw) + y * Math.Cos(yaw);
        double z1 = z;
        double y2 = y1 * Math.Cos(pitch) - z1 * Math.Sin(pitch);
        double viewHalfX = aspect >= 1 ? half * aspect : half;
        double viewHalfY = aspect >= 1 ? half : half / aspect;
        float sx = (float)((x1 / viewHalfX + 1) * 0.5 * _glControl.Width);
        float sy = (float)((1 - (y2 / viewHalfY + 1) * 0.5) * _glControl.Height);
        return new PointF(sx, sy);
    }

    private void AddToolbarButton(string text, EventHandler handler)
    {
        var button = new ToolStripButton(text);
        button.Click += handler;
        _toolbar.Items.Add(button);
    }

    private void AddToolbarToggle(string text, bool initialValue, Action<ToolStripButton> changed)
    {
        var button = new ToolStripButton(text) { CheckOnClick = true, Checked = initialValue };
        button.CheckedChanged += (_, _) =>
        {
            changed(button);
            InvalidateViewer();
        };
        _toolbar.Items.Add(button);
    }

    private void FitToModel()
    {
        _zoom = 1.0f;
        InvalidateViewer();
    }

    private void SetView(float yaw, float pitch)
    {
        _yaw = yaw;
        _pitch = pitch;
        InvalidateViewer();
    }

    public void RefreshView()
    {
        InvalidateViewer();
    }

    private void InvalidateViewer()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(InvalidateViewer));
            return;
        }

        UpdateViewerSurface();
        if (_forceFallback2D)
        {
            _fallbackPanel.Invalidate();
            _fallbackPanel.Update();
            return;
        }

        if (!_glControl.IsHandleCreated)
            return;

        try
        {
            if (_glControl.HasValidContext)
                _glControl.MakeCurrent();
            _glControl.Invalidate();
            _glControl.Update();
        }
        catch (InvalidOperationException)
        {
            _glControl.Invalidate();
        }
    }

    private void UpdateViewerSurface()
    {
        _fallbackPanel.Visible = _forceFallback2D;
        _glControl.Visible = !_forceFallback2D;

        if (_forceFallback2D)
            _fallbackPanel.BringToFront();
        else
            _glControl.BringToFront();
    }

    private static (Point3D Center, double Span) GetBounds(IReadOnlyList<Node> nodes)
    {
        double minX = nodes.Min(n => n.Position.X);
        double maxX = nodes.Max(n => n.Position.X);
        double minY = nodes.Min(n => n.Position.Y);
        double maxY = nodes.Max(n => n.Position.Y);
        double minZ = nodes.Min(n => n.Position.Z);
        double maxZ = nodes.Max(n => n.Position.Z);
        double span = Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ);
        if (span < 1e-9) span = 1.0;

        return (new Point3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2), span);
    }

    private static double GetModelSpan(IReadOnlyList<Node> nodes) => GetBounds(nodes).Span;
}

public enum ViewerColorMode
{
    Utilization,
    AxialForce,
    Moment,
    Material,
    Section
}
