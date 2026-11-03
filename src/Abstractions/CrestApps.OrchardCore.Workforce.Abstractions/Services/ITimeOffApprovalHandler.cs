using CrestApps.OrchardCore.Workforce.Models;

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

    /// <summary>
    /// Runs when approved time off is canceled, before the request is saved, to undo what <see cref="ApprovedAsync"/>
    /// did.
    /// </summary>
    /// <param name="request">The request being canceled.</param>
    /// <param name="actor">Who canceled it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task CanceledAsync(TimeOffRequest request, string actor, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
