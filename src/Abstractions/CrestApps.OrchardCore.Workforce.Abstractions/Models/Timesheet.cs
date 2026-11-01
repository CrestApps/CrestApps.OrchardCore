using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A worker's hours for one pay period, worked out from their punches, time off and premiums, which they attest to and
/// a manager approves.
/// </summary>
public sealed class Timesheet : CatalogItem
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the worker's team.
    /// </summary>
    public string TeamId { get; set; }

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
    /// Gets or sets where it stands.
    /// </summary>
    public TimesheetStatus Status { get; set; } = TimesheetStatus.Open;

    /// <summary>
    /// Gets or sets the lines.
    /// </summary>
    public IList<TimesheetLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the minutes worked.
    /// </summary>
    public int WorkedMinutes { get; set; }

    /// <summary>
    /// Gets or sets the open attendance exceptions.
    /// </summary>
    public int OpenExceptions { get; set; }

    /// <summary>
    /// Gets or sets what the worker attested to.
    /// </summary>
    public string Attestation { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the worker submitted it.
    /// </summary>
    public DateTime? SubmittedUtc { get; set; }

    /// <summary>
    /// Gets or sets who approved or rejected it.
    /// </summary>
    public string DecidedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was approved or rejected.
    /// </summary>
    public DateTime? DecidedUtc { get; set; }

    /// <summary>
    /// Gets or sets why it was rejected.
    /// </summary>
    public string RejectionReason { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was locked.
    /// </summary>
    public DateTime? LockedUtc { get; set; }

    /// <summary>
    /// Gets or sets the payroll exports it was in.
    /// </summary>
    public IList<string> ExportBatchIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC time its hours were last worked out.
    /// </summary>
    public DateTime? CalculatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets whether its hours still change with punches.
    /// </summary>
    public bool IsEditable => Status is TimesheetStatus.Open or TimesheetStatus.Rejected or TimesheetStatus.Reopened;

    /// <summary>
    /// Gets the total hours.
    /// </summary>
    public decimal TotalHours => Lines.Sum(line => line.Hours);
}
