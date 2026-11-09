namespace CrestApps.OrchardCore.Tabular;

/// <summary>
/// A row of a CSV file, read by column name in any case. A missing column reads as <see langword="null"/>.
/// </summary>
public sealed class CsvRow
{
    private readonly Dictionary<string, string> _values;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvRow"/> class.
    /// </summary>
    /// <param name="number">The row's line in the file, counting the header as 1.</param>
    /// <param name="values">The values by column name.</param>
    public CsvRow(int number, IReadOnlyDictionary<string, string> values)
    {
        Number = number;
        _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (column, value) in values ?? new Dictionary<string, string>())
        {
            _values.TryAdd(column, value);
        }
    }

    /// <summary>
    /// Gets the row's line in the file, counting the header as 1.
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// Gets the value of a column, or <see langword="null"/> when the file has no such column.
    /// </summary>
    /// <param name="column">The column name, in any case.</param>
    public string this[string column]
        => _values.GetValueOrDefault(column);
}
