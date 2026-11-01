namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Runs when time off is approved, such as Shift Planning taking the worker's overlapping shifts off the schedule.
/// </summary>
public interface ITimeOffApprovalHandler
{
    /// <summary>
    /// Handles approved time off.
    /// </summary>
    /// <param name="context">The time off and the shifts it overlaps.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ApprovedAsync(TimeOffApprovedContext context, CancellationToken cancellationToken = default);
}
