namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What a payroll CSV column holds.
/// </summary>
public enum PayrollCsvField
{
    /// <summary>
    /// The worker's id in the payroll system.
    /// </summary>
    WorkerExternalId,

    /// <summary>
    /// The employee number.
    /// </summary>
    EmployeeNumber,

    /// <summary>
    /// The worker's name.
    /// </summary>
    WorkerName,

    /// <summary>
    /// The pay code payroll knows.
    /// </summary>
    PayCode,

    /// <summary>
    /// The day.
    /// </summary>
    Date,

    /// <summary>
    /// The hours.
    /// </summary>
    Hours,

    /// <summary>
    /// The hourly rate.
    /// </summary>
    Rate,

    /// <summary>
    /// The hours times the rate and the code's multiplier, plus any premium.
    /// </summary>
    Amount,

    /// <summary>
    /// The first day of the pay period.
    /// </summary>
    PeriodStart,

    /// <summary>
    /// The last day of the pay period.
    /// </summary>
    PeriodEnd,

    /// <summary>
    /// The activity.
    /// </summary>
    ActivityCode,

    /// <summary>
    /// The job.
    /// </summary>
    Job,
}
