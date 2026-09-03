namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Supplies the selectable handoff-queue options for the subject AI-settings editor. Implemented by the Contact
/// Center (which owns queues) and resolved optionally, so the subject-flow editor can offer a queue picker when
/// Contact Center is enabled without the Omnichannel management module taking a compile-time dependency on it.
/// When no implementation is registered, the editor falls back to a free-text queue id.
/// </summary>
public interface IHandoffQueueOptionsProvider
{
    /// <summary>
    /// Gets the selectable handoff queues, ordered for display.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The queue options.</returns>
    Task<IReadOnlyList<HandoffQueueOption>> GetQueuesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A selectable handoff queue: its identifier and display name.
/// </summary>
public sealed class HandoffQueueOption
{
    /// <summary>
    /// Gets or sets the queue identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the queue display name.
    /// </summary>
    public string Name { get; set; }
}
