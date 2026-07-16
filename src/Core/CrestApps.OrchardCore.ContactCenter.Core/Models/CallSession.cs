using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the Contact Center's business-oriented projection of a voice call. It maps a provider
/// call to an interaction, agent, and queue, and tracks the normalized call lifecycle and durations
/// without owning media execution, which remains the responsibility of the Telephony provider.
/// </summary>
public sealed class CallSession : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the identifier of the interaction this call session belongs to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the CRM activity the call belongs to.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the provider that owns the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific identifier of the call.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the delivery model used to deliver the call to the agent.
    /// </summary>
    public VoiceProviderDeliveryModel DeliveryModel { get; set; }

    /// <summary>
    /// Gets or sets the direction of the call relative to the contact center.
    /// </summary>
    public InteractionDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the normalized call state.
    /// </summary>
    public ContactCenterCallState State { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent connected to the call.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the active agent-session identifier that owns the live media leg for this call.
    /// </summary>
    public string AgentSessionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue the call was delivered from.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the provider bridge identifier that currently joins the customer and agent media legs.
    /// </summary>
    public string BridgeId { get; set; }

    /// <summary>
    /// Gets or sets the provider conference identifier when the call is represented as a multi-party conference.
    /// </summary>
    public string ConferenceId { get; set; }

    /// <summary>
    /// Gets or sets the provider recording identifier for the active or retained call recording.
    /// </summary>
    public string RecordingId { get; set; }

    /// <summary>
    /// Gets or sets the supervisor agent identifier when a supervisor monitoring leg is attached.
    /// </summary>
    public string SupervisorAgentId { get; set; }

    /// <summary>
    /// Gets or sets the provider call-leg identifier used by the active supervisor monitoring leg.
    /// </summary>
    public string SupervisorLegId { get; set; }

    /// <summary>
    /// Gets or sets the durable provider-command identifier that last fenced a topology transition.
    /// </summary>
    public string DurableCommandId { get; set; }

    /// <summary>
    /// Gets or sets the address of the calling party.
    /// </summary>
    public string FromAddress { get; set; }

    /// <summary>
    /// Gets or sets the address of the called party.
    /// </summary>
    public string ToAddress { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the call is currently on hold.
    /// </summary>
    public bool IsOnHold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider reports the call as muted.
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Gets or sets the provider-reported recording state for the call.
    /// </summary>
    public RecordingState RecordingState { get; set; }

    /// <summary>
    /// Gets or sets the provider recording reference for the call, when one exists.
    /// </summary>
    public string RecordingReference { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider reports the call as a conference or
    /// multi-party session.
    /// </summary>
    public bool IsConference { get; set; }

    /// <summary>
    /// Gets or sets the number of active participants the provider reports for the call.
    /// </summary>
    public int ParticipantCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the latest provider event applied to this call session.
    /// </summary>
    public DateTime? LastProviderEventUtc { get; set; }

    /// <summary>
    /// Gets or sets the highest provider-supplied monotonic sequence number applied to this call
    /// session. When a provider supplies monotonic sequence numbers this value is the authoritative
    /// ordering high-water mark; deliveries at or below it are rejected as stale. It remains
    /// <see langword="null"/> for providers that only supply timestamps or idempotency keys.
    /// </summary>
    public long? HighWaterSequence { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the call session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the call started dialing or ringing.
    /// </summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the call was answered or connected.
    /// </summary>
    public DateTime? AnsweredUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the call ended.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets the total seconds the call was connected (talk time).
    /// </summary>
    public double TalkSeconds { get; set; }

    /// <summary>
    /// Gets or sets the total seconds the call spent on hold.
    /// </summary>
    public double HoldSeconds { get; set; }

    /// <summary>
    /// Gets or sets provider-specific metadata retained for troubleshooting.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the UTC time the call session was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
