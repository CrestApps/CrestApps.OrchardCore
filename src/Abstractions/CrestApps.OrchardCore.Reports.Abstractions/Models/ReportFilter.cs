using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Represents the filter applied when running a report. Every report shares the built-in from/to date
/// range; additional, report-specific filters are contributed through display drivers and stored in the
/// extensible entity <see cref="Entity.Properties"/> bag.
/// </summary>
public sealed class ReportFilter : Entity
{
    /// <summary>
    /// Gets or sets the technical name of the report being filtered. Filter display drivers use this to
    /// decide whether they apply to the current report.
    /// </summary>
    public string ReportName { get; set; }

    /// <summary>
    /// Gets or sets the inclusive lower UTC bound of the reporting period.
    /// </summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the inclusive upper UTC bound of the reporting period.
    /// </summary>
    public DateTime? ToUtc { get; set; }
}
