using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a communication event associated with a CRM activity. The CRM activity remains the
/// universal work item; an interaction captures the technical communication history for one attempt.
/// </summary>
public sealed class Interaction : CatalogItem, IEntity, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets extensible Orchard entity metadata for the interaction.
    /// </summary>
    public JsonObject EntityProperties { get; set; } = [];

    JsonObject IEntity.Properties
    {
        get => EntityProperties;
    }

    /// <summary>
    /// Gets or sets the channel the interaction is conducted on.
    /// </summary>
    public InteractionChannel Channel { get; set; }

    /// <summary>
    /// Gets or sets the direction of the interaction relative to the contact center.
    /// </summary>
    public InteractionDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the communication-session status of the interaction.
    /// </summary>
    public InteractionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the CRM activity this communication event belongs to.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the provider name that produced the communication event.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider interaction or call identifier.
    /// </summary>
    public string ProviderInteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider call leg identifier when the channel has leg-level tracking.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets the customer address used for the communication event.
    /// </summary>
    public string CustomerAddress { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center queue that handled the communication event, when applicable.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent connected to the communication event.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier shared by every event and provider session of this interaction.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the recording reference when a provider or media store captures the interaction.
    /// </summary>
    public string RecordingReference { get; set; }

    /// <summary>
    /// Gets or sets the recording state of the interaction.
    /// </summary>
    public RecordingState RecordingState { get; set; }

    /// <summary>
    /// Gets or sets the transcript reference when a transcript is available for the interaction.
    /// </summary>
    public string TranscriptReference { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier used by the provider webhook or callback, when different from <see cref="CorrelationId"/>.
    /// </summary>
    public string ProviderCorrelationId { get; set; }

    /// <summary>
    /// Gets or sets provider or channel-specific metadata that should remain attached to the interaction history.
    /// </summary>
    public IDictionary<string, object> TechnicalMetadata { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the queue transitions that occurred during the interaction.
    /// </summary>
    public IList<InteractionQueueHistoryEntry> QueueHistory { get; set; } = [];

    /// <summary>
    /// Gets or sets the transfer attempts that occurred during the interaction.
    /// </summary>
    public IList<InteractionTransferHistoryEntry> TransferHistory { get; set; } = [];

    /// <summary>
    /// Gets or sets the provider call legs that were associated with the interaction.
    /// </summary>
    public IList<InteractionCallLeg> CallLegs { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifier of the user that created the interaction.
    /// </summary>
    public string CreatedById { get; set; }

    /// <summary>
    /// Gets or sets the user name of the user that created the interaction.
    /// </summary>
    public string CreatedByUserName { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the interaction was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the interaction was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time work on the interaction started.
    /// </summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the interaction was answered or connected.
    /// </summary>
    public DateTime? AnsweredUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the interaction's communication session ended.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets the participants involved in the interaction.
    /// </summary>
    public IList<InteractionParticipant> Participants { get; set; } = [];
}
