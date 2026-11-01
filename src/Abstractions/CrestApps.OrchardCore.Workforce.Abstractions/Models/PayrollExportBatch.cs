using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// One payroll export: the locked timesheets it held and the file it made.
/// </summary>
public sealed class PayrollExportBatch : CatalogItem
{
    /// <summary>
    /// Gets or sets the provider that made it.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the pay periods.
    /// </summary>
    public string PayPeriodCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the first day of the period.
    /// </summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the last day of the period.
    /// </summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the timesheets.
    /// </summary>
    public IList<string> TimesheetIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows written.
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the file's media type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the file, so it can be downloaded again.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets who ran it.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it ran.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
