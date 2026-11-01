using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// What a payroll export provider writes.
/// </summary>
public sealed class PayrollExportContext
{
    /// <summary>
    /// Gets or sets the pay periods.
    /// </summary>
    public PayPeriodCalendar Calendar { get; set; }

    /// <summary>
    /// Gets or sets the first day of the period.
    /// </summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the last day of the period.
    /// </summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the locked timesheets.
    /// </summary>
    public IReadOnlyList<Timesheet> Timesheets { get; set; } = [];

    /// <summary>
    /// Gets or sets the timesheets' workers, by id.
    /// </summary>
    public IReadOnlyDictionary<string, WorkerProfile> Workers { get; set; }

    /// <summary>
    /// Gets or sets the pay codes, by id.
    /// </summary>
    public IReadOnlyDictionary<string, PayCode> PayCodes { get; set; }
}

/// <summary>
/// A file a payroll export provider made.
/// </summary>
public sealed class PayrollExportFile
{
    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the media type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets how many rows it has.
    /// </summary>
    public int RowCount { get; set; }
}
