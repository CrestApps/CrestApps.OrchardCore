namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Proposes which workers work which shift templates on which days to cover a team's coverage requirements. A
/// solver never writes: the proposal is applied through <see cref="ISchedulePlanningService"/>. A built-in solver
/// ships with Shift Planning; another module can add a better one.
/// </summary>
public interface IShiftScheduleSolver
{
    /// <summary>
    /// Gets the solver's technical name, such as <c>Default</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Solves a problem.
    /// </summary>
    /// <param name="problem">The problem, which the solver doesn't change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The proposed shifts and how well they cover the demand.</returns>
    Task<ShiftScheduleProposal> SolveAsync(ShiftScheduleProblem problem, CancellationToken cancellationToken = default);
}
