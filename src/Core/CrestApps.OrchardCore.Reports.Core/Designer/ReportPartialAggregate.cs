namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// The aggregates of one field over a group of rows that a data source grouped itself (see
/// <see cref="CrestApps.OrchardCore.Reports.DataSources.IReportAggregateDataSource"/>). The engine keeps one per group
/// in place of the rows' values, and <see cref="ReportAggregations"/> merges them, so totals, charts, and pivots that
/// regroup the result stay exact.
/// </summary>
public sealed class ReportPartialAggregate
{
    /// <summary>
    /// Gets or sets the number of rows with a value.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Gets or sets the sum of the values, or <see langword="null"/>.
    /// </summary>
    public object Sum { get; set; }

    /// <summary>
    /// Gets or sets the smallest value, or <see langword="null"/>.
    /// </summary>
    public object Min { get; set; }

    /// <summary>
    /// Gets or sets the largest value, or <see langword="null"/>.
    /// </summary>
    public object Max { get; set; }
}
