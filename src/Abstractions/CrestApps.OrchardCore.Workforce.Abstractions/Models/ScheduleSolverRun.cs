namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What the last auto-schedule run of a scenario did.
/// </summary>
public sealed class ScheduleSolverRun
{
    /// <summary>
    /// Gets or sets the solver that ran.
    /// </summary>
    public string Solver { get; set; }

    /// <summary>
    /// Gets or sets the share of the wanted people the schedule covers after the run, from 0 to 1.
    /// </summary>
    public double CoverageScore { get; set; }

    /// <summary>
    /// Gets or sets the share of the fewest people needed the schedule covers after the run, from 0 to 1.
    /// </summary>
    public double MinimumScore { get; set; }

    /// <summary>
    /// Gets or sets how many shifts the run added.
    /// </summary>
    public int ShiftsAdded { get; set; }

    /// <summary>
    /// Gets or sets how long it ran, in milliseconds.
    /// </summary>
    public long RuntimeMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets whether it stopped at its time limit.
    /// </summary>
    public bool TimedOut { get; set; }

    /// <summary>
    /// Gets or sets when it ran.
    /// </summary>
    public DateTime RunUtc { get; set; }

    /// <summary>
    /// Gets or sets who ran it.
    /// </summary>
    public string RunBy { get; set; }
}
