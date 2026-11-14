namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// A line of pay another module hands to payroll: hours or an amount under a pay code, for a staff member and a pay
/// period, such as the regular hours of a locked timesheet.
/// </summary>
public sealed class PayInput
{
    /// <summary>
    /// Gets or sets the kind of record the input comes from, such as Timesheet. It names the source.
    /// </summary>
    public string SourceKind { get; set; }

    /// <summary>
    /// Gets or sets the id of the record the input comes from. Every input of one record is consumed together.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the staff member paid.
    /// </summary>
    public string StaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets the staff member's name, as the source knows it.
    /// </summary>
    public string StaffMemberName { get; set; }

    /// <summary>
    /// Gets or sets the staff member's employee number.
    /// </summary>
    public string EmployeeNumber { get; set; }

    /// <summary>
    /// Gets or sets the id an outside payroll system knows the staff member by, or the employee number.
    /// </summary>
    public string PayrollId { get; set; }

    /// <summary>
    /// Gets or sets the pay schedule the period belongs to.
    /// </summary>
    public string PayScheduleId { get; set; }

    /// <summary>
    /// Gets or sets the first day of the pay period.
    /// </summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the last day of the pay period.
    /// </summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the pay code the input is paid under.
    /// </summary>
    public string PayCodeId { get; set; }

    /// <summary>
    /// Gets or sets the day the input is for, when it is for one day.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// Gets or sets the hours, when the input is hours.
    /// </summary>
    public decimal? Hours { get; set; }

    /// <summary>
    /// Gets or sets the amount, when the input is an amount, such as a bonus.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the rate an hour in effect on the day, as the source knows it, or <see langword="null"/>.
    /// </summary>
    public decimal? Rate { get; set; }

    /// <summary>
    /// Gets or sets where the cost goes: the department and site teams, the activity and the job.
    /// </summary>
    public PayInputAllocation Allocation { get; set; } = new();

    /// <summary>
    /// Gets or sets whether a pay run or an export already took the input.
    /// </summary>
    public bool Consumed { get; set; }
}

/// <summary>
/// Where the cost of a pay input goes.
/// </summary>
public sealed class PayInputAllocation
{
    /// <summary>
    /// Gets or sets the department team.
    /// </summary>
    public string DepartmentTeamId { get; set; }

    /// <summary>
    /// Gets or sets the site team.
    /// </summary>
    public string SiteTeamId { get; set; }

    /// <summary>
    /// Gets or sets the activity code.
    /// </summary>
    public string ActivityCode { get; set; }

    /// <summary>
    /// Gets or sets the job.
    /// </summary>
    public string Job { get; set; }
}

/// <summary>
/// What pay inputs to read.
/// </summary>
public sealed class PayInputQuery
{
    /// <summary>
    /// Gets or sets the employer, or <see langword="null"/> for every one.
    /// </summary>
    public string EmployerId { get; set; }

    /// <summary>
    /// Gets or sets the pay schedule, or <see langword="null"/> for every one.
    /// </summary>
    public string PayScheduleId { get; set; }

    /// <summary>
    /// Gets or sets the earliest period start to read, or <see langword="null"/>.
    /// </summary>
    public DateOnly? PeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the latest period start to read, or <see langword="null"/>.
    /// </summary>
    public DateOnly? PeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the staff members to read, or none for every one.
    /// </summary>
    public IReadOnlyCollection<string> StaffMemberIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether inputs a pay run or an export already took are read too.
    /// </summary>
    public bool IncludeConsumed { get; set; }
}

/// <summary>
/// The record a pay input comes from.
/// </summary>
/// <param name="SourceKind">The kind of record.</param>
/// <param name="SourceId">The record's id.</param>
public sealed record PayInputReference(string SourceKind, string SourceId);

/// <summary>
/// The records a pay run or an export took, or gives back.
/// </summary>
public sealed class PayInputConsumption
{
    /// <summary>
    /// Gets or sets who took them: a pay run's id, or a data pipeline run's.
    /// </summary>
    public string ConsumerId { get; set; }

    /// <summary>
    /// Gets or sets the records.
    /// </summary>
    public IReadOnlyCollection<PayInputReference> Sources { get; set; } = [];
}
