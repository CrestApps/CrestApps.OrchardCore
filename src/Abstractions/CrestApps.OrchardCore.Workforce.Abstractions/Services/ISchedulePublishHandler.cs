namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Hears about a published schedule, in the same request, before the session commits; notifications, labor rules and
/// workflows listen here.
/// </summary>
public interface ISchedulePublishHandler
{
    /// <summary>
    /// Called after a period was published.
    /// </summary>
    /// <param name="context">The publish.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task PublishedAsync(SchedulePublishedContext context, CancellationToken cancellationToken = default);
}
