namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// Describes the rows the report engine asks a data source to read from one data set.
/// </summary>
public sealed class ReportDataSourceQuery
{
    /// <summary>
    /// Gets or sets the technical name of the data set to read.
    /// </summary>
    public string DataSet { get; set; }

    /// <summary>
    /// Gets or sets the technical names of the fields the report uses. A data source returns at least these fields
    /// and may skip the work of computing any other field. When empty, every field is needed.
    /// </summary>
    public ISet<string> Fields { get; set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the filter conditions the source may apply while reading. See <see cref="ReportDataCondition"/>.
    /// </summary>
    public IList<ReportDataCondition> Conditions { get; set; } = [];

    /// <summary>
    /// Gets or sets the most rows the source may return. A source that has more rows returns this many and sets
    /// <see cref="ReportDataTable.Truncated"/>.
    /// </summary>
    public int MaxRows { get; set; }

    /// <summary>
    /// Gets or sets the caller context of the run.
    /// </summary>
    public ReportDataSourceContext Context { get; set; } = new();
}
