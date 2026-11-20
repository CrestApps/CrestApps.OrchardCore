namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// The rows of a file a <see cref="IContentTransferRecordImporter"/> imports at once, in file order.
/// </summary>
public sealed class ContentTransferImportBatch
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentTransferImportBatch"/> class.
    /// </summary>
    /// <param name="entry">The import.</param>
    /// <param name="rows">The rows.</param>
    public ContentTransferImportBatch(ContentTransferEntry entry, IReadOnlyList<ContentTransferImportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Entry = entry;
        Rows = rows ?? [];
    }

    /// <summary>
    /// Gets the import. A target may keep what it needs from one batch to the next, and across a resume, in its
    /// properties: they are saved with the progress after each batch.
    /// </summary>
    public ContentTransferEntry Entry { get; }

    /// <summary>
    /// Gets the rows, in file order.
    /// </summary>
    public IReadOnlyList<ContentTransferImportRow> Rows { get; }
}

/// <summary>
/// One row of an imported file, read by column name.
/// </summary>
public sealed class ContentTransferImportRow
{
    private readonly Dictionary<string, string> _values;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentTransferImportRow"/> class.
    /// </summary>
    /// <param name="rowIndex">The row's number in the file, the first row under the header being 1.</param>
    /// <param name="values">The cells by column name; a cell with no value is <see langword="null"/>.</param>
    public ContentTransferImportRow(int rowIndex, IEnumerable<KeyValuePair<string, string>> values)
    {
        RowIndex = rowIndex;
        _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (column, value) in values ?? [])
        {
            if (!string.IsNullOrEmpty(column))
            {
                _values.TryAdd(column.Trim(), value);
            }
        }
    }

    /// <summary>
    /// Gets the row's number in the file, the first row under the header being 1.
    /// </summary>
    public int RowIndex { get; }

    /// <summary>
    /// Gets the names of the file's columns.
    /// </summary>
    public IEnumerable<string> Columns => _values.Keys;

    /// <summary>
    /// Gets why the row was not imported, or <see langword="null"/> when it was.
    /// </summary>
    public string Error { get; private set; }

    /// <summary>
    /// Gets the kind of error, which the import counts rows by, such as <c>Did not pass validation</c>.
    /// </summary>
    public string ErrorCategory { get; private set; }

    /// <summary>
    /// Gets whether the row was failed.
    /// </summary>
    public bool Failed => Error is not null;

    /// <summary>
    /// Gets the value of a column in any case, or of the first of its other names that the file has, or
    /// <see langword="null"/> when the file has none of them or the cell is empty.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <param name="otherNames">Other names the column may have.</param>
    /// <returns>The trimmed value.</returns>
    public string Get(string column, params string[] otherNames)
    {
        foreach (var name in otherNames is { Length: > 0 } ? [column, .. otherNames] : new[] { column })
        {
            if (_values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Gets whether the file has a column, in any case.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    public bool Has(string column)
        => _values.ContainsKey(column);

    /// <summary>
    /// Fails the row: it is not imported, and the file of failed rows gives the reason.
    /// </summary>
    /// <param name="error">Why the row was not imported.</param>
    /// <param name="category">The kind of error, or <see langword="null"/> for <c>Could not be imported</c>.</param>
    public void Fail(string error, string category = null)
    {
        Error = string.IsNullOrWhiteSpace(error) ? "The row failed to import." : error;
        ErrorCategory = category;
    }
}
