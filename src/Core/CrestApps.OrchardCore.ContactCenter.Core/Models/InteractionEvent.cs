using System.Text.Json;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a single durable Contact Center domain event. Interaction events form the auditable,
/// replayable history of everything that happens to an interaction across the contact center.
/// </summary>
public sealed class InteractionEvent : Entity
{
    /// <summary>
    /// Gets or sets the stable identifier of the event.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the interaction the event belongs to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the canonical event type name. See <see cref="ContactCenterConstants.Events"/>.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the schema version of the event payload.
    /// </summary>
    public int SchemaVersion { get; set; } = ContactCenterConstants.CurrentEventSchemaVersion;

    /// <summary>
    /// Gets or sets the type of aggregate the event applies to, such as the interaction or a queue item.
    /// </summary>
    public string AggregateType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the aggregate the event applies to.
    /// </summary>
    public string AggregateId { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier shared by every event of the same interaction.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the event that caused this event, when known.
    /// </summary>
    public string CausationId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the actor that originated the event, or a system actor.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets the name of the component that originated the event. See <see cref="ContactCenterConstants.Components"/>.
    /// </summary>
    public string SourceComponent { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the event occurred.
    /// </summary>
    public DateTime OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets an optional idempotency key used to de-duplicate provider-originated events.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON payload of the event.
    /// </summary>
    public string Data { get; set; }

    /// <summary>
    /// Deserializes the event payload into the specified type.
    /// </summary>
    /// <typeparam name="T">The payload type to deserialize into.</typeparam>
    /// <returns>The deserialized payload, or the default value when no payload is present.</returns>
    public T GetData<T>()
    {
        if (string.IsNullOrEmpty(Data))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(Data);
    }

    /// <summary>
    /// Serializes the specified payload and stores it as the event data.
    /// </summary>
    /// <typeparam name="T">The payload type to serialize.</typeparam>
    /// <param name="payload">The payload to serialize.</param>
    public void SetData<T>(T payload)
    {
        Data = payload is null
            ? null
            : JsonSerializer.Serialize(payload);
    }
}
