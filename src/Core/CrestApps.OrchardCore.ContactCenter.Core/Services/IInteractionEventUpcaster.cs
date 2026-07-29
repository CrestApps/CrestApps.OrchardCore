using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Converts the payload of a persisted Contact Center domain event from one schema version to the next.
/// A durable event log outlives the code that wrote it: an event published months ago is redelivered by the
/// outbox, replayed by a projection, and read by a report long after its payload shape has changed. Without a
/// conversion the stored JSON is deserialized straight into today's type, so a renamed, split, or re-united
/// property arrives as its default value and the handler acts on a payload that is quietly wrong rather than
/// failing. One implementation converts exactly one version step for one event type, and the steps are chained
/// until the payload reaches <see cref="ContactCenterConstants.CurrentEventSchemaVersion"/>.
/// </summary>
public interface IInteractionEventUpcaster
{
    /// <summary>
    /// Gets the canonical event type this upcaster converts, or <see langword="null"/> to convert the payload of
    /// every event type at <see cref="FromVersion"/>. See <see cref="ContactCenterConstants.Events"/>.
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Gets the schema version this upcaster converts from. It always produces the payload at
    /// <see cref="FromVersion"/> plus one, because a step that skipped a version would leave a hole no other
    /// upcaster could be written to fill.
    /// </summary>
    int FromVersion { get; }

    /// <summary>
    /// Converts a payload written at <see cref="FromVersion"/> into the payload shape of the next version.
    /// </summary>
    /// <param name="payload">The stored payload, or <see langword="null"/> when the event carries no payload.</param>
    /// <returns>The converted payload, or <see langword="null"/> when the event carries no payload.</returns>
    JsonNode Upcast(JsonNode payload);
}
