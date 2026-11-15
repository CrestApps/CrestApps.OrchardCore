using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.SensitiveData;

namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// How an employer pays a staff member: the schedule, the worker type, the protected tax id, the dated tax elections,
/// how net pay is split, where the cost goes and the status. One per staff member and employer, kept in the Payroll
/// collection and never on the staff member, so modules that load staff never load tax ids.
/// </summary>
public sealed class EmployeePayProfile : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the staff member paid.
    /// </summary>
    public string StaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets the employer paying.
    /// </summary>
    public string EmployerId { get; set; }

    /// <summary>
    /// Gets or sets the pay schedule.
    /// </summary>
    public string PayScheduleId { get; set; }

    /// <summary>
    /// Gets or sets whether the person is an employee or a contractor.
    /// </summary>
    public PayWorkerType WorkerType { get; set; }

    /// <summary>
    /// Gets or sets whether overtime is paid.
    /// </summary>
    public OvertimeStatus OvertimeStatus { get; set; }

    /// <summary>
    /// Gets or sets whether the person is paid a cash wage below the minimum wage and the tips they receive make up the
    /// rest (a tip credit): a week whose cash wage and tips fall short is made up to the minimum.
    /// </summary>
    public bool Tipped { get; set; }

    /// <summary>
    /// Gets or sets the person's tax id, protected; only its last characters show.
    /// </summary>
    public ProtectedValue TaxId { get; set; }

    /// <summary>
    /// Gets or sets a keyed hash of the tax id, to find a number used twice without reading it.
    /// </summary>
    public string TaxIdHash { get; set; }

    /// <summary>
    /// Gets or sets the legal name printed on statements and forms, when it differs from the staff member's name.
    /// </summary>
    public string LegalName { get; set; }

    /// <summary>
    /// Gets or sets the home address, for taxes and forms.
    /// </summary>
    public Address HomeAddress { get; set; }

    /// <summary>
    /// Gets or sets the work site, a site team whose jurisdictions apply to the work.
    /// </summary>
    public string WorkSiteTeamId { get; set; }

    /// <summary>
    /// Gets or sets the tax elections, each for a jurisdiction from a day on. The history is kept.
    /// </summary>
    public IList<TaxElection> Elections { get; set; } = [];

    /// <summary>
    /// Gets or sets how net pay is split, in order; the last row takes the remainder.
    /// </summary>
    public IList<NetPayRow> Distribution { get; set; } = [];

    /// <summary>
    /// Gets or sets where the cost goes when an input says nothing.
    /// </summary>
    public PayInputAllocation DefaultAllocation { get; set; } = new();

    /// <summary>
    /// Gets or sets the first day retro pay may reach back to, or <see langword="null"/> for no limit.
    /// </summary>
    public DateOnly? RetroPayFrom { get; set; }

    /// <summary>
    /// Gets or sets whether the person is paid.
    /// </summary>
    public PayProfileStatus Status { get; set; } = PayProfileStatus.Active;

    /// <summary>
    /// Gets or sets when it was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets the election of a jurisdiction in effect on a day.
    /// </summary>
    /// <param name="jurisdiction">The jurisdiction.</param>
    /// <param name="day">The day.</param>
    /// <returns>The election, or <see langword="null"/>.</returns>
    public TaxElection ElectionOn(string jurisdiction, DateOnly day)
        => Elections
            .Where(election => string.Equals(election.Jurisdiction, jurisdiction, StringComparison.OrdinalIgnoreCase) && election.EffectiveFrom <= day)
            .OrderByDescending(election => election.EffectiveFrom)
            .ThenByDescending(election => election.EnteredUtc)
            .FirstOrDefault();
}

/// <summary>
/// Whether a person is an employee or a contractor.
/// </summary>
public enum PayWorkerType
{
    /// <summary>
    /// An employee: taxes are withheld.
    /// </summary>
    Employee,

    /// <summary>
    /// A contractor: paid without withholding.
    /// </summary>
    Contractor,
}

/// <summary>
/// Whether overtime is paid.
/// </summary>
public enum OvertimeStatus
{
    /// <summary>
    /// Overtime is paid.
    /// </summary>
    NonExempt,

    /// <summary>
    /// Overtime is not paid.
    /// </summary>
    Exempt,
}

/// <summary>
/// Whether a person is paid.
/// </summary>
public enum PayProfileStatus
{
    /// <summary>
    /// Paid in pay runs.
    /// </summary>
    Active,

    /// <summary>
    /// On leave: left out of regular pay runs.
    /// </summary>
    OnLeave,

    /// <summary>
    /// No longer paid, but for a final pay run.
    /// </summary>
    Terminated,
}

/// <summary>
/// A person's tax election for a jurisdiction from a day on: the values of the jurisdiction's field set, such as a
/// filing status and extra withholding.
/// </summary>
public sealed class TaxElection
{
    /// <summary>
    /// Gets or sets the jurisdiction, such as US or US-CA.
    /// </summary>
    public string Jurisdiction { get; set; }

    /// <summary>
    /// Gets or sets the first day it applies.
    /// </summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the values, by field name.
    /// </summary>
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets who entered it.
    /// </summary>
    public string EnteredBy { get; set; }

    /// <summary>
    /// Gets or sets when it was entered.
    /// </summary>
    public DateTime EnteredUtc { get; set; }
}

/// <summary>
/// A row of the net pay split: a bank account or a check, for an amount, a percent or the remainder.
/// </summary>
public sealed class NetPayRow
{
    /// <summary>
    /// Gets or sets how the row is paid.
    /// </summary>
    public NetPayMethod Method { get; set; }

    /// <summary>
    /// Gets or sets the bank account, for a deposit.
    /// </summary>
    public string BankAccountId { get; set; }

    /// <summary>
    /// Gets or sets how much the row takes.
    /// </summary>
    public NetPaySplit Split { get; set; } = NetPaySplit.Remainder;

    /// <summary>
    /// Gets or sets the amount, for an amount row.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the percent of net pay, for a percent row.
    /// </summary>
    public decimal? Percent { get; set; }
}

/// <summary>
/// How a row of net pay is paid.
/// </summary>
public enum NetPayMethod
{
    /// <summary>
    /// A deposit to a bank account.
    /// </summary>
    BankAccount,

    /// <summary>
    /// A printed check.
    /// </summary>
    Check,
}

/// <summary>
/// How much a row of net pay takes.
/// </summary>
public enum NetPaySplit
{
    /// <summary>
    /// A fixed amount.
    /// </summary>
    Amount,

    /// <summary>
    /// A percent of net pay.
    /// </summary>
    Percent,

    /// <summary>
    /// What is left.
    /// </summary>
    Remainder,
}

/// <summary>
/// A recurring earning or deduction of a pay profile under a pay code: an amount or a percent, dated, with an employer
/// match, a goal, maximums and arrears.
/// </summary>
public sealed class EmployeePayItem : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the pay profile.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the staff member, copied from the profile.
    /// </summary>
    public string StaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets the employer, copied from the profile.
    /// </summary>
    public string EmployerId { get; set; }

    /// <summary>
    /// Gets or sets the pay code.
    /// </summary>
    public string PayCodeId { get; set; }

    /// <summary>
    /// Gets or sets the amount each pay period, or <see langword="null"/> for a percent.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the percent of gross pay, or <see langword="null"/> for an amount.
    /// </summary>
    public decimal? Percent { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day, or <see langword="null"/> for no end.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the percent the employer matches, such as for a retirement plan.
    /// </summary>
    public decimal? EmployerMatchPercent { get; set; }

    /// <summary>
    /// Gets or sets the most the employer matches, as a percent of gross pay.
    /// </summary>
    public decimal? EmployerMatchLimitPercent { get; set; }

    /// <summary>
    /// Gets or sets the total to reach, such as a loan, after which the item stops.
    /// </summary>
    public decimal? Goal { get; set; }

    /// <summary>
    /// Gets or sets how much was paid toward the goal.
    /// </summary>
    public decimal PaidTowardGoal { get; set; }

    /// <summary>
    /// Gets or sets the most taken in a pay period.
    /// </summary>
    public decimal? PeriodMaximum { get; set; }

    /// <summary>
    /// Gets or sets the most taken in a year.
    /// </summary>
    public decimal? YearMaximum { get; set; }

    /// <summary>
    /// Gets or sets what could not be taken and is owed.
    /// </summary>
    public decimal ArrearsBalance { get; set; }

    /// <summary>
    /// Gets or sets a note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets when it was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Whether the item applies on a day.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>Whether it applies.</returns>
    public bool AppliesOn(DateOnly day)
        => StartDate <= day && (EndDate is null || EndDate >= day) && (Goal is null || PaidTowardGoal < Goal);
}
