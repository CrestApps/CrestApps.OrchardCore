using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// What <see cref="ITimesheetCalculator"/> works a timesheet out from.
/// </summary>
public sealed class TimesheetCalculationInput
{
    /// <summary>
    /// Gets or sets the policy.
    /// </summary>
    public TimeClockPolicy Policy { get; set; }

    /// <summary>
    /// Gets or sets the worker's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the first day of the period.
    /// </summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the last day of the period.
    /// </summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the day the overtime week starts.
    /// </summary>
    public DayOfWeek WeekStart { get; set; } = DayOfWeek.Sunday;

    /// <summary>
    /// Gets or sets the worker's punches, corrections already applied, from the start of the period's first
    /// overtime week.
    /// </summary>
    public IReadOnlyList<TimePunch> Punches { get; set; } = [];

    /// <summary>
    /// Gets or sets the worker's published shifts.
    /// </summary>
    public IReadOnlyList<TimesheetShift> Shifts { get; set; } = [];

    /// <summary>
    /// Gets or sets the worker's approved time off.
    /// </summary>
    public IReadOnlyList<TimesheetTimeOff> TimeOff { get; set; } = [];

    /// <summary>
    /// Gets or sets the worker's approved predictability pay.
    /// </summary>
    public IReadOnlyList<PredictabilityPayEntry> PredictabilityPay { get; set; } = [];

    /// <summary>
    /// Gets or sets the pay codes.
    /// </summary>
    public IReadOnlyList<PayCode> PayCodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the lines a manager added.
    /// </summary>
    public IReadOnlyList<TimesheetLine> ManualLines { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC time now.
    /// </summary>
    public DateTime NowUtc { get; set; }
}

/// <summary>
/// A published shift, for grace and attendance.
/// </summary>
public sealed class TimesheetShift
{
    /// <summary>
    /// Gets or sets the shift's key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets when it starts.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ends.
    /// </summary>
    public DateTime EndUtc { get; set; }
}

/// <summary>
/// Approved time off on one day.
/// </summary>
public sealed class TimesheetTimeOff
{
    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the paid hours.
    /// </summary>
    public decimal Hours { get; set; }

    /// <summary>
    /// Gets or sets the pay code; empty for the paid time off code.
    /// </summary>
    public string PayCodeId { get; set; }

    /// <summary>
    /// Gets or sets the request.
    /// </summary>
    public string RequestId { get; set; }
}
