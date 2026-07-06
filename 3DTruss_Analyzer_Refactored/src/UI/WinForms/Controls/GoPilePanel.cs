namespace TrussAnalyzer.UI.WinForms.Controls;

using System.Globalization;
using TrussAnalyzer.Core.Design.Foundation;
using Forms = System.Windows.Forms;

public sealed class GoPilePanel : Forms.UserControl
{
    private readonly GoPileCalculator _calculator = new();
    private readonly FoundationHelixView _view = new();
    private readonly Forms.ComboBox _type = new() { DropDownStyle = Forms.ComboBoxStyle.DropDownList };
    private readonly Forms.DataGridView _results = new() { Dock = Forms.DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = Forms.DataGridViewAutoSizeColumnsMode.Fill };
    private readonly Dictionary<string, Forms.NumericUpDown> _inputs = new();
    private readonly Forms.Label _summary = new() { Dock = Forms.DockStyle.Top, Height = 34, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };

    public GoPilePanel()
    {
        Dock = Forms.DockStyle.Fill;
        BuildLayout();
        CalculateAndRender();
    }

    private void BuildLayout()
    {
        var split = new Forms.SplitContainer { Dock = Forms.DockStyle.Fill, SplitterDistance = 430 };
        Controls.Add(split);

        var left = new Forms.TableLayoutPanel { Dock = Forms.DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        left.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        left.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 300));
        left.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
        left.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Percent, 100));
        split.Panel1.Controls.Add(left);

        var header = new Forms.Label
        {
            Dock = Forms.DockStyle.Top,
            Height = 32,
            Text = "GO Pile - Eccentric Pile Foundation",
            Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold)
        };
        left.Controls.Add(header, 0, 0);

        var inputGrid = new Forms.TableLayoutPanel { Dock = Forms.DockStyle.Fill, ColumnCount = 2, AutoScroll = true };
        inputGrid.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 55));
        inputGrid.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 45));
        left.Controls.Add(inputGrid, 0, 1);

        AddTypeInput(inputGrid);
        AddNumberInput(inputGrid, "Pu (kN)", "Pu", 1000, 0, 100000, 10);
        AddNumberInput(inputGrid, "Mx (kN-m)", "Mx", 0, -100000, 100000, 10);
        AddNumberInput(inputGrid, "My (kN-m)", "My", 0, -100000, 100000, 10);
        AddNumberInput(inputGrid, "Column ex (m)", "ex", 0.15m, -10, 10, 0.01m);
        AddNumberInput(inputGrid, "Column ey (m)", "ey", 0, -10, 10, 0.01m);
        AddNumberInput(inputGrid, "Pile spacing X (m)", "sx", 1.5m, 0.1m, 20, 0.05m);
        AddNumberInput(inputGrid, "Pile spacing Y (m)", "sy", 1.5m, 0.1m, 20, 0.05m);
        AddNumberInput(inputGrid, "Pile cap. comp. (kN)", "capC", 350, 1, 100000, 10);
        AddNumberInput(inputGrid, "Pile cap. tension (kN)", "capT", 0, 0, 100000, 10);
        AddNumberInput(inputGrid, "Footing Lx (m)", "lx", 2.4m, 0.1m, 50, 0.05m);
        AddNumberInput(inputGrid, "Footing Ly (m)", "ly", 2.4m, 0.1m, 50, 0.05m);
        AddNumberInput(inputGrid, "Thickness (m)", "thk", 0.6m, 0.1m, 5, 0.05m);
        AddNumberInput(inputGrid, "Column bx (m)", "cbx", 0.3m, 0.05m, 5, 0.05m);
        AddNumberInput(inputGrid, "Column by (m)", "cby", 0.3m, 0.05m, 5, 0.05m);

        var buttonPanel = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Fill, FlowDirection = Forms.FlowDirection.RightToLeft };
        var calculate = new Forms.Button { Text = "Calculate", Width = 110 };
        calculate.Click += (_, _) => CalculateAndRender();
        buttonPanel.Controls.Add(calculate);
        left.Controls.Add(buttonPanel, 0, 2);

        _results.Columns.Add("Pile", "Pile");
        _results.Columns.Add("X", "X (m)");
        _results.Columns.Add("Y", "Y (m)");
        _results.Columns.Add("Reaction", "Reaction (kN)");
        _results.Columns.Add("Util", "Util.");
        _results.Columns.Add("Status", "Status");

        var resultPanel = new Forms.Panel { Dock = Forms.DockStyle.Fill };
        resultPanel.Controls.Add(_results);
        resultPanel.Controls.Add(_summary);
        left.Controls.Add(resultPanel, 0, 3);

        split.Panel2.Controls.Add(_view);
    }

    private void AddTypeInput(Forms.TableLayoutPanel grid)
    {
        _type.Items.AddRange(Enum.GetNames<GoPileFoundationType>());
        _type.SelectedItem = GoPileFoundationType.F4.ToString();
        _type.SelectedIndexChanged += (_, _) => CalculateAndRender();
        AddRow(grid, "Foundation type", _type);
    }

    private void AddNumberInput(Forms.TableLayoutPanel grid, string label, string key, decimal value, decimal minimum, decimal maximum, decimal increment)
    {
        var input = new Forms.NumericUpDown
        {
            DecimalPlaces = increment < 1 ? 2 : 0,
            Increment = increment,
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            Dock = Forms.DockStyle.Fill,
            ThousandsSeparator = true
        };
        input.ValueChanged += (_, _) => CalculateAndRender();
        _inputs[key] = input;
        AddRow(grid, label, input);
    }

    private static void AddRow(Forms.TableLayoutPanel grid, string label, Forms.Control control)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 28));
        grid.Controls.Add(new Forms.Label { Text = label, Dock = Forms.DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private void CalculateAndRender()
    {
        try
        {
            var result = _calculator.Calculate(ReadInput());
            PopulateResults(result);
            _view.SetResult(result);
        }
        catch (Exception ex)
        {
            _summary.Text = $"GO Pile input error: {ex.Message}";
            _summary.ForeColor = System.Drawing.Color.Firebrick;
            _view.SetResult(null);
        }
    }

    private GoPileInput ReadInput()
    {
        var type = Enum.TryParse<GoPileFoundationType>(Convert.ToString(_type.SelectedItem, CultureInfo.InvariantCulture), out var parsed)
            ? parsed
            : GoPileFoundationType.F4;

        return new GoPileInput
        {
            FoundationType = type,
            AxialCompression = Kilo("Pu"),
            MomentX = Kilo("Mx"),
            MomentY = Kilo("My"),
            ColumnOffsetX = Value("ex"),
            ColumnOffsetY = Value("ey"),
            PileSpacingX = Value("sx"),
            PileSpacingY = Value("sy"),
            PileCapacityCompression = Kilo("capC"),
            PileCapacityTension = Kilo("capT"),
            FootingLengthX = Value("lx"),
            FootingWidthY = Value("ly"),
            FootingThickness = Value("thk"),
            ColumnSizeX = Value("cbx"),
            ColumnSizeY = Value("cby")
        };
    }

    private void PopulateResults(GoPileResult result)
    {
        _summary.Text = $"{result.Input.FoundationType}: {(result.OverallPass ? "PASS" : "NG")} | Max compression {result.MaxCompression / 1000:F1} kN | Mx {result.AppliedMomentX / 1000:F1} kN-m | My {result.AppliedMomentY / 1000:F1} kN-m | As-X {FormatRebar(result.ReinforcementX)} | As-Y {FormatRebar(result.ReinforcementY)}";
        _summary.ForeColor = result.OverallPass ? System.Drawing.Color.ForestGreen : System.Drawing.Color.Firebrick;
        _results.Rows.Clear();

        foreach (var pile in result.Piles)
        {
            int rowIndex = _results.Rows.Add(
                $"P{pile.Id}",
                pile.Position.X.ToString("F3", CultureInfo.InvariantCulture),
                pile.Position.Y.ToString("F3", CultureInfo.InvariantCulture),
                (pile.Reaction / 1000.0).ToString("F2", CultureInfo.InvariantCulture),
                pile.CompressionUtilization.ToString("F3", CultureInfo.InvariantCulture),
                pile.CompressionPass && pile.TensionPass ? "OK" : "NG");
            _results.Rows[rowIndex].DefaultCellStyle.BackColor = pile.CompressionPass && pile.TensionPass
                ? System.Drawing.Color.Honeydew
                : System.Drawing.Color.MistyRose;
        }
    }

    private double Value(string key) => (double)_inputs[key].Value;
    private double Kilo(string key) => Value(key) * 1000.0;

    private static string FormatRebar(ReinforcementDesignResult result)
    {
        return $"DB{result.BarDiameter * 1000:F0}@{result.BarSpacing * 1000:F0}";
    }
}

