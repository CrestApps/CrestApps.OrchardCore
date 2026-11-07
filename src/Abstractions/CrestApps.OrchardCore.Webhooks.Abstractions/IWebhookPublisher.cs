namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Sends an event to every webhook subscription that takes it. The event is queued in the request's unit of work, sent
/// once that work is saved, and retried with backoff through the delivery outbox until the receiver accepts it.
/// </summary>
/// <remarks>
/// The publisher is registered by the Webhooks feature. A module publishes from a startup gated on that feature (or on
/// a feature that depends on it), or resolves the publisher with <c>GetService</c> and skips when it is null. Only
/// events an enabled module declares through an <see cref="IWebhookEventProvider"/> are sent.
/// </remarks>
public interface IWebhookPublisher
{
    /// <summary>
    /// Queues an event, under a new id, for every enabled subscription that takes it. Never throws.
    /// </summary>
    /// <param name="eventName">The event's name.</param>
    /// <param name="payload">What the event is about, serialized as JSON in the body's <c>data</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the deliveries are queued.</returns>
    Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken = default)
        => PublishAsync(eventName, payload, eventId: null, cancellationToken);

    /// <summary>
    /// Queues an event under an id of the publisher's: an event that can be raised twice for one thing keeps one id,
    /// so a receiver drops the repeat. Never throws: a webhook is a notification and must not fail the work that raised
    /// it.
    /// </summary>
    /// <param name="eventName">The event's name.</param>
    /// <param name="payload">What the event is about, serialized as JSON in the body's <c>data</c>.</param>
    /// <param name="eventId">The event's id, or <see langword="null"/> for a new one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the deliveries are queued.</returns>
    Task PublishAsync(string eventName, object payload, string eventId, CancellationToken cancellationToken = default);
}
