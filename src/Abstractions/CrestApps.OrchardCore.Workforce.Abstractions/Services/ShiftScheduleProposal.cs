namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// The shifts an <see cref="IShiftScheduleSolver"/> proposes, and how well the schedule covers the demand with them.
/// </summary>
public sealed class ShiftScheduleProposal
{
    /// <summary>
    /// Gets or sets the proposed shifts, besides the fixed ones.
    /// </summary>
    public IList<ProposedShift> Shifts { get; set; } = [];

    /// <summary>
    /// Gets or sets the share of the wanted people covered, from 0 to 1, counting the fixed shifts.
    /// </summary>
    public double CoverageScore { get; set; }

    /// <summary>
    /// Gets or sets the share of the fewest people needed covered, from 0 to 1, counting the fixed shifts.
    /// </summary>
    public double MinimumScore { get; set; }

    /// <summary>
    /// Gets or sets the solver that made it.
    /// </summary>
    public string Solver { get; set; }

    /// <summary>
    /// Gets or sets how long it took.
    /// </summary>
    public TimeSpan Runtime { get; set; }

    /// <summary>
    /// Gets or sets whether the solver stopped at its time limit.
    /// </summary>
    public bool TimedOut { get; set; }
}

/// <summary>
/// A proposed shift.
/// </summary>
/// <param name="WorkerId">The worker.</param>
/// <param name="Day">The day, from 0.</param>
/// <param name="TemplateId">The template.</param>
public sealed record ProposedShift(string WorkerId, int Day, string TemplateId);
