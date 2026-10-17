using System.Security.Claims;

namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// Carries the caller information a report data source needs to decide which data sets it may expose.
/// </summary>
public sealed class ReportDataSourceContext
{
    /// <summary>
    /// Gets or sets the principal the data is read for. While a report is designed this is the designer; while a
    /// saved report runs it is the report owner, because the owner vouches for the data the report shows. A data
    /// source must hide every data set this principal is not allowed to read.
    /// </summary>
    public ClaimsPrincipal User { get; set; }

    /// <summary>
    /// Gets the extensible bag of values the report engine and data sources share during one run. The report views
    /// data source uses it to detect views that read themselves.
    /// </summary>
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
