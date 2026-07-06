namespace TrussAnalyzer.UI.WinForms.Controls;

using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using TrussAnalyzer.Core.Design.Foundation;
using Forms = System.Windows.Forms;
using MediaPoint3D = System.Windows.Media.Media3D.Point3D;

public sealed class FoundationHelixView : Forms.UserControl
{
    private readonly ElementHost _host = new() { Dock = Forms.DockStyle.Fill };
    private readonly HelixViewport3D _viewport = new()
    {
        ShowCoordinateSystem = true,
        ShowViewCube = true,
        ZoomExtentsWhenLoaded = true,
        Background = new LinearGradientBrush(Color.FromRgb(236, 246, 255), Colors.White, 90)
    };
    private readonly ModelVisual3D _scene = new();

    public FoundationHelixView()
    {
        Dock = Forms.DockStyle.Fill;
        _host.Child = _viewport;
        Controls.Add(_host);
        _viewport.Children.Add(new SunLight());
        _viewport.Children.Add(_scene);
        SetCamera();
    }

    public void SetResult(GoPileResult? result)
    {
        _scene.Children.Clear();
        if (result == null)
        {
            AddText("GO Pile", new MediaPoint3D(0, 0, 0.5), Brushes.DimGray);
            return;
        }

        AddFooting(result);
        AddColumn(result);
        AddPiles(result);
        AddAxes(result);
        AddSummary(result);
        _viewport.ZoomExtents();
    }

    private void AddFooting(GoPileResult result)
    {
        var input = result.Input;
        _scene.Children.Add(new BoxVisual3D
        {
            Center = new MediaPoint3D(0, 0, input.FootingThickness / 2.0),
            Width = input.FootingLengthX,
            Length = input.FootingWidthY,
            Height = input.FootingThickness,
            Fill = Brushes.LightSlateGray
        });
    }

    private void AddColumn(GoPileResult result)
    {
        var input = result.Input;
        _scene.Children.Add(new BoxVisual3D
        {
            Center = new MediaPoint3D(input.ColumnOffsetX, input.ColumnOffsetY, input.FootingThickness + 0.45),
            Width = input.ColumnSizeX,
            Length = input.ColumnSizeY,
            Height = 0.9,
            Fill = Brushes.DarkSlateGray
        });
        AddText("Column", new MediaPoint3D(input.ColumnOffsetX, input.ColumnOffsetY, input.FootingThickness + 1.0), Brushes.Black);
    }

    private void AddPiles(GoPileResult result)
    {
        double maxAbs = Math.Max(1, result.Piles.Max(p => Math.Abs(p.Reaction)));
        foreach (var pile in result.Piles)
        {
            var brush = pile.Reaction < -1e-9
                ? Brushes.DarkViolet
                : pile.CompressionPass ? Brushes.SeaGreen : Brushes.Firebrick;

            _scene.Children.Add(new PipeVisual3D
            {
                Point1 = new MediaPoint3D(pile.Position.X, pile.Position.Y, -1.2),
                Point2 = new MediaPoint3D(pile.Position.X, pile.Position.Y, 0),
                Diameter = 0.28,
                Fill = brush
            });

            double arrowHeight = 0.25 + 0.75 * Math.Abs(pile.Reaction) / maxAbs;
            var start = pile.Reaction >= 0
                ? new MediaPoint3D(pile.Position.X, pile.Position.Y, result.Input.FootingThickness + arrowHeight)
                : new MediaPoint3D(pile.Position.X, pile.Position.Y, -0.2);
            var end = pile.Reaction >= 0
                ? new MediaPoint3D(pile.Position.X, pile.Position.Y, result.Input.FootingThickness + 0.05)
                : new MediaPoint3D(pile.Position.X, pile.Position.Y, -0.2 + arrowHeight);

            _scene.Children.Add(new ArrowVisual3D
            {
                Point1 = start,
                Point2 = end,
                Diameter = 0.055,
                Fill = brush
            });

            AddText($"P{pile.Id} {FormatForce(pile.Reaction)}", new MediaPoint3D(pile.Position.X, pile.Position.Y, result.Input.FootingThickness + 0.16), brush);
        }
    }

    private void AddAxes(GoPileResult result)
    {
        double span = Math.Max(result.Input.FootingLengthX, result.Input.FootingWidthY) * 0.6;
        _scene.Children.Add(new ArrowVisual3D { Point1 = new MediaPoint3D(0, 0, 0), Point2 = new MediaPoint3D(span, 0, 0), Fill = Brushes.Red, Diameter = 0.035 });
        _scene.Children.Add(new ArrowVisual3D { Point1 = new MediaPoint3D(0, 0, 0), Point2 = new MediaPoint3D(0, span, 0), Fill = Brushes.Green, Diameter = 0.035 });
        _scene.Children.Add(new ArrowVisual3D { Point1 = new MediaPoint3D(0, 0, 0), Point2 = new MediaPoint3D(0, 0, span), Fill = Brushes.Blue, Diameter = 0.035 });
        AddText("+X", new MediaPoint3D(span, 0, 0), Brushes.Red);
        AddText("+Y", new MediaPoint3D(0, span, 0), Brushes.Green);
        AddText("+Z", new MediaPoint3D(0, 0, span), Brushes.Blue);
    }

    private void AddSummary(GoPileResult result)
    {
        string status = result.OverallPass ? "PASS" : "NG";
        var brush = result.OverallPass ? Brushes.ForestGreen : Brushes.Firebrick;
        AddText($"{result.Input.FoundationType} {status} | Max={FormatForce(result.MaxCompression)}", new MediaPoint3D(-result.Input.FootingLengthX / 2, -result.Input.FootingWidthY / 2, result.Input.FootingThickness + 0.45), brush);
    }

    private void SetCamera()
    {
        _viewport.Camera = new PerspectiveCamera
        {
            Position = new MediaPoint3D(4, -5, 3),
            LookDirection = new Vector3D(-4, 5, -2.5),
            UpDirection = new Vector3D(0, 0, 1),
            FieldOfView = 45
        };
    }

    private void AddText(string text, MediaPoint3D position, Brush brush)
    {
        _scene.Children.Add(new BillboardTextVisual3D
        {
            Text = text,
            Position = position,
            Foreground = brush,
            Background = Brushes.White
        });
    }

    private static string FormatForce(double force)
    {
        return Math.Abs(force) >= 1000 ? $"{force / 1000:F1} kN" : $"{force:F0} N";
    }
}

