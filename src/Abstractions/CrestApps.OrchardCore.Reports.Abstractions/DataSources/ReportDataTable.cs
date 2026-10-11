namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// The rows a data source returns for one data set. Each row holds one value per entry of <see cref="Fields"/>, in
/// the same order, using the CLR type documented on <see cref="ReportDataType"/>.
/// </summary>
public sealed class ReportDataTable
{
    /// <summary>
    /// Gets or sets the fields of the table, aligned by index with each row.
    /// </summary>
    public IList<ReportFieldDescriptor> Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows of the table.
    /// </summary>
    public IList<object[]> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the source had more rows than it was allowed to return.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Finds the index of a field by its technical name.
    /// </summary>
    /// <param name="name">The technical field name.</param>
    /// <returns>The zero-based index, or <c>-1</c> when the table has no such field.</returns>
    public int IndexOf(string name)
    {
        for (var index = 0; index < Fields.Count; index++)
        {
            if (string.Equals(Fields[index].Name, name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
