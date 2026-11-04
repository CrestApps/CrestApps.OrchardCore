namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Hears the things that happen in Workforce, named in <see cref="WorkforceEvents"/>, after they are saved. With the
/// Workflows feature on, they start workflows; otherwise nothing hears them.
/// </summary>
public interface IWorkforceEvents
{
    /// <summary>
    /// Raises an event.
    /// </summary>
    /// <param name="name">The event's name, from <see cref="WorkforceEvents"/>.</param>
    /// <param name="input">What happened: the ids of the worker, team and record.</param>
    /// <param name="correlationId">The record the event is about.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task RaiseAsync(string name, IDictionary<string, object> input, string correlationId, CancellationToken cancellationToken = default);
}
