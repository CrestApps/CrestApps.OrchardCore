namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// The defaults of shift planning, until labor rule sets replace them.
/// </summary>
public sealed class ShiftPlanningOptions
{
    /// <summary>
    /// Gets or sets the fewest hours between a worker's shifts, for auto-scheduling, claims and trades.
    /// </summary>
    public int MinRestHours { get; set; } = 10;

    /// <summary>
    /// Gets or sets the most days in a row the auto-scheduler gives a worker.
    /// </summary>
    public int MaxConsecutiveDays { get; set; } = 6;

    /// <summary>
    /// Gets or sets the most paid hours in a week for a worker with no maximum or contracted hours.
    /// </summary>
    public int DefaultMaxHoursPerWeek { get; set; } = 40;

    /// <summary>
    /// Gets or sets how long the auto-scheduler may run, in seconds.
    /// </summary>
    public int SolverTimeLimitSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the solver used when none is chosen.
    /// </summary>
    public string DefaultSolver { get; set; } = WorkforceConstants.DefaultSolver;

    /// <summary>
    /// Gets or sets how long an open shift given by seniority takes claims before it is awarded, in hours.
    /// </summary>
    public int SeniorityClaimHours { get; set; } = 24;
}
