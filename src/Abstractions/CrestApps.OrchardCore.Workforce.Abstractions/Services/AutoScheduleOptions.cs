namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// How to auto-schedule a scenario.
/// </summary>
public sealed class AutoScheduleOptions
{
    /// <summary>
    /// Gets or sets the solver to use; <see langword="null"/> for the configured one.
    /// </summary>
    public string Solver { get; set; }

    /// <summary>
    /// Gets or sets the length of the coverage intervals.
    /// </summary>
    public int IntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets how long the solver may run, in seconds; <see langword="null"/> for the configured limit.
    /// </summary>
    public int? TimeLimitSeconds { get; set; }

    /// <summary>
    /// Gets or sets the seed that makes a run repeatable.
    /// </summary>
    public int Seed { get; set; } = 1;
}
