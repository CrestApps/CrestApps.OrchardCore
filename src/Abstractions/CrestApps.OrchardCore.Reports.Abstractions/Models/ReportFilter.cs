using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Represents the filter applied when running a report. A report declares no fixed dimensions of its own;
/// every filter (including the built-in date range) is contributed through display drivers and stored in
/// the extensible entity <see cref="Entity.Properties"/> bag, so reports that do not need a given filter
/// simply do not contribute it. Use the <see cref="ReportFilterExtensions"/> helpers to read and write
/// typed values.
/// </summary>
public sealed class ReportFilter : Entity
{
    /// <summary>
    /// Gets or sets the technical name of the report being filtered. Filter display drivers use this to
    /// decide whether they apply to the current report.
    /// </summary>
    public string ReportName { get; set; }
}
