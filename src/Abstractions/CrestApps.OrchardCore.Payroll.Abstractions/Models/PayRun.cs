using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// One payroll of one employer: a period, a pay date, who is paid, the type, the totals and blockers of its last
/// calculation, and where it is in its life (<see cref="PayRunStatuses.Transitions"/>). Its draft statements are
/// calculated from the profiles, the inputs of every source and the run's own inputs; approval goes through
/// Approvals, and a change after approval returns the run to calculated.
/// </summary>
public sealed class PayRun : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>Gets or sets the number, given when the run is added.</summary>
    public string Number { get; set; }

    /// <summary>Gets or sets the employer.</summary>
    public string EmployerId { get; set; }

    /// <summary>Gets or sets the pay schedule whose profiles are paid.</summary>
    public string PayScheduleId { get; set; }

    /// <summary>Gets or sets the type.</summary>
    public PayRunType Type { get; set; }

    /// <summary>Gets or sets the first day of the period.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Gets or sets the last day of the period.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Gets or sets the pay date.</summary>
    public DateOnly PayDate { get; set; }

    /// <summary>Gets or sets the staff members paid, or none for every active profile of the employer on the schedule.</summary>
    public IList<string> StaffMemberIds { get; set; } = [];

    /// <summary>Gets or sets how supplemental pay is withheld.</summary>
    public PayrollSupplementalMethod SupplementalMethod { get; set; }

    /// <summary>Gets or sets the totals of the last calculation.</summary>
    public PayRunTotals Totals { get; set; } = new();

    /// <summary>Gets or sets what the last calculation found: blockers stop the run from being submitted.</summary>
    public IList<PayRunIssue> Issues { get; set; } = [];

    /// <summary>Gets or sets a stamp of the last calculation: an approval is for this version only.</summary>
    public string Version { get; set; }

    /// <summary>Gets or sets the approval request, while there is one.</summary>
    public string ApprovalRequestId { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public PayRunStatus Status { get; set; }

    /// <summary>Gets or sets the user who last calculated or submitted the run.</summary>
    public string PreparedById { get; set; }

    /// <summary>Gets or sets the name of that user.</summary>
    public string PreparedBy { get; set; }

    /// <summary>Gets or sets when it was last calculated.</summary>
    public DateTime? CalculatedUtc { get; set; }

    /// <summary>Gets or sets when it was approved.</summary>
    public DateTime? ApprovedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was last processed.
    /// </summary>
    public DateTime? ProcessedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user name of who last processed it.
    /// </summary>
    public string ProcessedBy { get; set; }

    /// <summary>Gets or sets when it was added.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets whether the run's inputs and statements may still change (it was not processed, paid or canceled).
    /// </summary>
    public bool IsOpen
        => Status is PayRunStatus.Draft or PayRunStatus.Calculating or PayRunStatus.Calculated or PayRunStatus.PendingApproval or PayRunStatus.Approved;

    /// <summary>
    /// Gets whether the run pays salaries and pay items: a regular, final or correction run does; a supplemental,
    /// off-cycle or contractor run pays only its inputs.
    /// </summary>
    public bool IsRegular
        => Type is PayRunType.Regular or PayRunType.Final or PayRunType.Correction;
}

/// <summary>
/// The type of a pay run.
/// </summary>
public enum PayRunType
{
    /// <summary>The scheduled payroll: salaries, hours, pay items and inputs.</summary>
    Regular,

    /// <summary>A payroll outside the schedule, of inputs only.</summary>
    OffCycle,

    /// <summary>Bonuses or commissions, of inputs only, often withheld at the flat rate.</summary>
    Supplemental,

    /// <summary>Corrects an earlier run.</summary>
    Correction,

    /// <summary>The last pay of people who leave.</summary>
    Final,

    /// <summary>Reverses an earlier run.</summary>
    Reversal,

    /// <summary>Statements paid elsewhere, loaded so the accumulators start right.</summary>
    Historical,

    /// <summary>Contractors, with nothing withheld.</summary>
    Contractor,
}

/// <summary>
/// Where a pay run is in its life.
/// </summary>
public enum PayRunStatus
{
    /// <summary>Added, not calculated yet, or an input changed.</summary>
    Draft,

    /// <summary>Being calculated.</summary>
    Calculating,

    /// <summary>Calculated: the register can be reviewed and the run submitted.</summary>
    Calculated,

    /// <summary>Waiting for approval.</summary>
    PendingApproval,

    /// <summary>Approved: ready to process.</summary>
    Approved,

    /// <summary>Statements are being made final and paid.</summary>
    Processing,

    /// <summary>Every payment went out.</summary>
    Paid,

    /// <summary>Some payments went out, some failed.</summary>
    PartiallyPaid,

    /// <summary>Processing failed.</summary>
    Failed,

    /// <summary>Canceled before it was processed.</summary>
    Canceled,
}

/// <summary>
/// The moves a pay run may make.
/// </summary>
public static class PayRunStatuses
{
    private static readonly TransitionTable<PayRunStatus> _transitions = new(new Dictionary<PayRunStatus, PayRunStatus[]>
    {
        [PayRunStatus.Draft] = [PayRunStatus.Calculating, PayRunStatus.Canceled],
        [PayRunStatus.Calculating] = [PayRunStatus.Calculated, PayRunStatus.Draft],
        [PayRunStatus.Calculated] = [PayRunStatus.Calculating, PayRunStatus.PendingApproval, PayRunStatus.Draft, PayRunStatus.Canceled],
        [PayRunStatus.PendingApproval] = [PayRunStatus.Approved, PayRunStatus.Calculated, PayRunStatus.Calculating, PayRunStatus.Draft, PayRunStatus.Canceled],
        [PayRunStatus.Approved] = [PayRunStatus.Processing, PayRunStatus.Calculated, PayRunStatus.Calculating, PayRunStatus.Draft, PayRunStatus.Canceled],
        [PayRunStatus.Processing] = [PayRunStatus.Paid, PayRunStatus.PartiallyPaid, PayRunStatus.Failed],
        [PayRunStatus.Failed] = [PayRunStatus.Processing],
        [PayRunStatus.PartiallyPaid] = [PayRunStatus.Paid, PayRunStatus.Processing],
        [PayRunStatus.Paid] = [],
        [PayRunStatus.Canceled] = [],
    });

    /// <summary>
    /// Gets the table of moves: a run is calculated again from any open status, an approval is for one calculation,
    /// and a processed run never goes back (a partly paid run pays what is left).
    /// </summary>
    public static TransitionTable<PayRunStatus> Transitions
        => _transitions;
}

/// <summary>
/// The totals of a pay run's calculation.
/// </summary>
public sealed class PayRunTotals
{
    /// <summary>Gets or sets the statements.</summary>
    public int Statements { get; set; }

    /// <summary>Gets or sets the gross pay.</summary>
    public decimal Gross { get; set; }

    /// <summary>Gets or sets the employees' taxes.</summary>
    public decimal EmployeeTaxes { get; set; }

    /// <summary>Gets or sets the employer's taxes.</summary>
    public decimal EmployerTaxes { get; set; }

    /// <summary>Gets or sets the deductions.</summary>
    public decimal Deductions { get; set; }

    /// <summary>Gets or sets the employer's contributions.</summary>
    public decimal EmployerContributions { get; set; }

    /// <summary>Gets or sets the reimbursements.</summary>
    public decimal Reimbursements { get; set; }

    /// <summary>Gets or sets the net pay.</summary>
    public decimal Net { get; set; }

    /// <summary>Gets the cash the employer needs: net pay, both sides' taxes, deductions paid on, and contributions.</summary>
    public decimal CashNeeded
        => Net + EmployeeTaxes + EmployerTaxes + Deductions + EmployerContributions;
}

/// <summary>
/// Something a pay run's calculation found.
/// </summary>
public sealed class PayRunIssue
{
    /// <summary>Gets or sets the staff member it is about, or <see langword="null"/> for the run.</summary>
    public string StaffMemberId { get; set; }

    /// <summary>Gets or sets the message.</summary>
    public string Message { get; set; }

    /// <summary>Gets or sets a value indicating whether it stops the run from being submitted.</summary>
    public bool IsBlocker { get; set; }
}

/// <summary>
/// An hour or amount entered for one pay run: typed on the run's page or saved by the Save pay inputs pipeline step.
/// </summary>
public sealed class PayRunInput : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>Gets or sets the pay run.</summary>
    public string RunId { get; set; }

    /// <summary>Gets or sets the staff member.</summary>
    public string StaffMemberId { get; set; }

    /// <summary>Gets or sets the pay code.</summary>
    public string PayCodeId { get; set; }

    /// <summary>Gets or sets the day of the work, or <see langword="null"/>.</summary>
    public DateOnly? Date { get; set; }

    /// <summary>Gets or sets the hours.</summary>
    public decimal? Hours { get; set; }

    /// <summary>Gets or sets the rate an hour, or <see langword="null"/> for the person's rate.</summary>
    public decimal? Rate { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Gets or sets a note.</summary>
    public string Note { get; set; }

    /// <summary>Gets or sets where it came from, such as the run's page or a pipeline run.</summary>
    public string Origin { get; set; }

    /// <summary>Gets or sets when it was added.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }
}
