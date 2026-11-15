namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// An invoice made again and again on a schedule: the bill-to, lines and charges of each invoice, the recurrence rule,
/// when it starts and ends, and what each run does with the invoice.
/// </summary>
public sealed class RecurringPlan : InvoicingDocument
{
    /// <summary>
    /// Gets or sets the plan's name, for staff.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the recurrence rule (RFC 5545), such as <c>FREQ=MONTHLY;BYMONTHDAY=1</c>.
    /// </summary>
    public string Rule { get; set; }

    /// <summary>
    /// Gets or sets the day of the first run, a date in the site's time zone.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day a run can happen, if the plan ends on a date.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Gets or sets how many runs the plan makes in all, if it ends after a number of runs.
    /// </summary>
    public int? MaxRuns { get; set; }

    /// <summary>
    /// Gets or sets what each run does with the invoice it makes.
    /// </summary>
    public RecurringPlanMode Mode { get; set; }

    /// <summary>
    /// Gets or sets where the plan stands.
    /// </summary>
    public RecurringPlanStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the day of the next run, when the plan is active.
    /// </summary>
    public DateOnly? NextRunDate { get; set; }

    /// <summary>
    /// Gets or sets how many runs made an invoice.
    /// </summary>
    public int RunCount { get; set; }

    /// <summary>
    /// Gets or sets the payment term of the invoices it makes; the bill-to's or the site's when empty.
    /// </summary>
    public string PaymentTermId { get; set; }

    /// <summary>
    /// Gets or sets the percent the line prices rise by every <see cref="PriceIncreaseEveryRuns"/> runs, such as 3.
    /// </summary>
    public decimal? PriceIncreasePercent { get; set; }

    /// <summary>
    /// Gets or sets whether each run also bills the customer's unbilled charges up to its day (with Invoicing Billing).
    /// </summary>
    public bool BillUnbilledCharges { get; set; }

    /// <summary>
    /// Gets or sets how many runs pass between price rises, such as 12 for a yearly rise on a monthly plan.
    /// </summary>
    public int? PriceIncreaseEveryRuns { get; set; }

    /// <summary>
    /// Gets the runs so far, newest last: what each made and how it went.
    /// </summary>
    public IList<RecurringPlanRun> Runs { get; set; } = [];
}

/// <summary>
/// What a run of a recurring plan does with the invoice it makes.
/// </summary>
public enum RecurringPlanMode
{
    /// <summary>
    /// Leaves it a draft for staff to check and issue.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Issues it.
    /// </summary>
    Issue = 1,

    /// <summary>
    /// Issues it and e-mails it to the customer.
    /// </summary>
    IssueAndSend = 2,

    /// <summary>
    /// Issues it and charges the customer's saved payment method.
    /// </summary>
    IssueAndCharge = 3,
}

/// <summary>
/// Where a recurring plan stands.
/// </summary>
public enum RecurringPlanStatus
{
    /// <summary>
    /// Being set up; it makes nothing yet.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Making invoices on its schedule.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Stopped for now; the runs it would have made are skipped.
    /// </summary>
    Paused = 2,

    /// <summary>
    /// Made every run it was set up to make.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Stopped for good.
    /// </summary>
    Canceled = 4,
}

/// <summary>
/// One run of a recurring plan.
/// </summary>
public sealed class RecurringPlanRun
{
    /// <summary>
    /// Gets or sets the occurrence the run was for, a date in the site's time zone.
    /// </summary>
    public DateOnly Occurrence { get; set; }

    /// <summary>
    /// Gets or sets when the run happened, in UTC.
    /// </summary>
    public DateTime RunUtc { get; set; }

    /// <summary>
    /// Gets or sets how it went.
    /// </summary>
    public RecurringRunOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the invoice it made, if any.
    /// </summary>
    public string InvoiceId { get; set; }

    /// <summary>
    /// Gets or sets the invoice's number, once issued.
    /// </summary>
    public string InvoiceNumber { get; set; }

    /// <summary>
    /// Gets or sets what went wrong, or a note.
    /// </summary>
    public string Message { get; set; }
}

/// <summary>
/// How a run of a recurring plan went.
/// </summary>
public enum RecurringRunOutcome
{
    /// <summary>
    /// A draft invoice was made.
    /// </summary>
    Drafted = 0,

    /// <summary>
    /// The invoice was issued.
    /// </summary>
    Issued = 1,

    /// <summary>
    /// The invoice was issued and e-mailed.
    /// </summary>
    Sent = 2,

    /// <summary>
    /// The occurrence had an invoice already, so nothing was made.
    /// </summary>
    Skipped = 3,

    /// <summary>
    /// The run failed; its message says why. A draft it made stays for staff.
    /// </summary>
    Failed = 4,
}
