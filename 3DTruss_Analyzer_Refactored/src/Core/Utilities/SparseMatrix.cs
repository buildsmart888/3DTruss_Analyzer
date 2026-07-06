namespace TrussAnalyzer.Core.Utilities;

public sealed class SparseMatrix
{
    private readonly Dictionary<(int Row, int Column), double> _values = new();

    public SparseMatrix(int rowCount, int columnCount)
    {
        if (rowCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowCount), "Row count must be positive.");
        if (columnCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(columnCount), "Column count must be positive.");

        RowCount = rowCount;
        ColumnCount = columnCount;
    }

    public int RowCount { get; }
    public int ColumnCount { get; }
    public int NonZeroCount => _values.Count;

    public double this[int row, int column]
    {
        get
        {
            ValidateIndex(row, column);
            return _values.TryGetValue((row, column), out double value) ? value : 0.0;
        }
        set
        {
            ValidateIndex(row, column);
            var key = (row, column);
            if (Math.Abs(value) <= 1e-18)
                _values.Remove(key);
            else
                _values[key] = value;
        }
    }

    public IEnumerable<SparseMatrixEntry> Entries => _values
        .OrderBy(kvp => kvp.Key.Row)
        .ThenBy(kvp => kvp.Key.Column)
        .Select(kvp => new SparseMatrixEntry(kvp.Key.Row, kvp.Key.Column, kvp.Value));

    public static SparseMatrix FromDense(double[,] dense, double zeroTolerance = 1e-18)
    {
        ArgumentNullException.ThrowIfNull(dense);
        int rows = dense.GetLength(0);
        int columns = dense.GetLength(1);
        var sparse = new SparseMatrix(rows, columns);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                double value = dense[row, column];
                if (Math.Abs(value) > zeroTolerance)
                    sparse[row, column] = value;
            }
        }

        return sparse;
    }

    public double[,] ToDense()
    {
        var dense = new double[RowCount, ColumnCount];
        foreach (var entry in _values)
            dense[entry.Key.Row, entry.Key.Column] = entry.Value;
        return dense;
    }

    private void ValidateIndex(int row, int column)
    {
        if (row < 0 || row >= RowCount)
            throw new ArgumentOutOfRangeException(nameof(row), row, "Row index is outside the matrix.");
        if (column < 0 || column >= ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(column), column, "Column index is outside the matrix.");
    }
}

public readonly record struct SparseMatrixEntry(int Row, int Column, double Value);

