using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// The inputs of one run of a designed query besides the query itself.
/// </summary>
public sealed class ReportQueryExecutionContext
{
    /// <summary>
    /// Gets or sets the caller context passed to the data sources.
    /// </summary>
    public ReportDataSourceContext DataSourceContext { get; set; } = new();

    /// <summary>
    /// Gets the values the person running the report entered for exposed filters, by filter identifier. When a filter
    /// identifier is present, its values replace the filter's defaults; an empty list turns the filter off.
    /// </summary>
    public IDictionary<string, IList<string>> FilterValues { get; } = new Dictionary<string, IList<string>>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the current UTC date and time.
    /// </summary>
    public DateTime UtcNow { get; set; }

    /// <summary>
    /// Gets or sets the function that converts a UTC date-time to the tenant time zone.
    /// </summary>
    public Func<DateTime, DateTime> ToLocal { get; set; } = value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    /// <summary>
    /// Gets or sets the function that converts a date-time in the tenant time zone to UTC.
    /// </summary>
    public Func<DateTime, DateTime> ToUtc { get; set; } = value => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>
    /// Gets or sets the size limits of the run.
    /// </summary>
    public ReportQueryLimits Limits { get; set; } = new();
}
