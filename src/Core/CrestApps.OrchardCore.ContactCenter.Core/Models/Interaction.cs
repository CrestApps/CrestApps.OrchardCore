using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.Models;
using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
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
    /// Gets the communication-session status of the interaction. It is changed only through
    /// <see cref="TransitionTo(InteractionStatus)"/>, so a status the lifecycle does not admit cannot be
    /// recorded by any caller.
    /// </summary>
    [JsonInclude]
    public InteractionStatus Status { get; private set; }

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
    /// Gets or sets the UTC instant at which explicit party consent to record this interaction was captured, when
    /// the tenant recording governance policy requires it.
    /// </summary>
    public DateTime? RecordingConsentCapturedUtc { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction under which recording consent for this interaction was evaluated, when known.
    /// </summary>
    public string RecordingConsentJurisdiction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the captured recording is under legal hold. A recording under legal
    /// hold is exempt from retention-driven and subject-request erasure until the hold is released.
    /// </summary>
    public bool RecordingLegalHold { get; set; }

    /// <summary>
    /// Gets or sets the UTC instant beyond which the captured recording becomes eligible for erasure, or
    /// <see langword="null"/> when the recording is retained indefinitely.
    /// </summary>
    public DateTime? RecordingRetainUntilUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC instant at which the captured recording reference was erased at the orchestration layer
    /// in response to a right-to-erasure request, or <see langword="null"/> when the recording has not been erased.
    /// </summary>
    public DateTime? RecordingErasedUtc { get; set; }

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
    /// Gets or sets the UTC time after-call wrap-up started.
    /// </summary>
    public DateTime? WrapUpStartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time after-call wrap-up was completed.
    /// </summary>
    public DateTime? WrapUpCompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the participants involved in the interaction.
    /// </summary>
    public IList<InteractionParticipant> Participants { get; set; } = [];

    /// <summary>
    /// Moves the interaction to the specified communication-session status.
    /// </summary>
    /// <param name="status">The status to move to.</param>
    /// <exception cref="InvalidStateTransitionException">The interaction cannot reach the status from the one it is in.</exception>
    public void TransitionTo(InteractionStatus status)
    {
        if (!InteractionLifecycle.CanTransition(Status, status))
        {
            throw new InvalidStateTransitionException(nameof(Interaction), Status, status);
        }

        Status = status;
    }

    /// <summary>
    /// Restores a communication-session status that was decided elsewhere, without consulting the lifecycle.
    /// </summary>
    /// <param name="status">The status to restore.</param>
    /// <returns>The same interaction, so it can be used at the end of an object initializer.</returns>
    /// <remarks>
    /// This bypasses every transition rule and exists only so a test can put an interaction directly into the
    /// state it wants to exercise. Production code must never call it: <c>AggregateLifecycleArchitectureTests</c>
    /// fails the build if any file under <c>src/</c> does, so the bypass cannot quietly become a shortcut.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Interaction RestorePersistedStatus(InteractionStatus status)
    {
        Status = status;

        return this;
    }

    /// <summary>
    /// Mirrors the communication-session status implied by the authoritative call session.
    /// </summary>
    /// <param name="status">The status implied by the call session's current state.</param>
    /// <remarks>
    /// For a provider-backed voice interaction the call session is the authority on what the call is doing, and
    /// the interaction's status is a projection of it rather than an independent decision. Ordering for that
    /// stream is enforced upstream by <c>VoiceStreamOrdering</c>, which rejects deliveries that would move the
    /// call backwards; re-deciding the same question here with a second, narrower rule would let the two records
    /// disagree, which is the divergence <c>CallStateMachinePropertyTests</c> exists to catch. The lifecycle
    /// table therefore governs the paths where this system decides the next status, not the ones where it is
    /// reporting what a provider already did.
    /// </remarks>
    public void MirrorSessionStatus(InteractionStatus status)
    {
        // Mirroring reports what the provider already did, so it does not consult the table. It still refuses to
        // bring a settled interaction back to life: the interaction can settle on a path the call session never
        // sees, such as an offer released after the customer abandoned, and a late provider frame that reopened
        // it would put a finished conversation back into the agent's live work.
        if (InteractionLifecycle.IsSettled(Status) && !InteractionLifecycle.IsSettled(status))
        {
            return;
        }

        Status = status;
    }

    /// <summary>
    /// Returns the interaction to routing so it can be offered again, clearing the agent it was offered to.
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">The interaction's communication session has already settled.</exception>
    /// <remarks>
    /// This names the one thing several call sites were each spelling out for themselves. It moves along the
    /// declared backwards edge like any other transition, so it is refused once the session has settled: a
    /// settled status has no outgoing edge, and re-offering a call that is over creates work for a conversation
    /// nobody can join.
    /// </remarks>
    public void Requeue()
    {
        TransitionTo(InteractionStatus.Created);
        AgentId = null;
    }

    /// <summary>
    /// Returns the interaction to the alerting state so it can be offered to another agent.
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">The interaction's communication session has already settled.</exception>
    public void Reoffer()
    {
        TransitionTo(InteractionStatus.Ringing);
    }

    /// <summary>
    /// Determines whether the interaction can move to the specified communication-session status.
    /// </summary>
    /// <param name="status">The status to test.</param>
    /// <returns><see langword="true"/> when the transition is admitted; otherwise <see langword="false"/>.</returns>
    public bool CanTransitionTo(InteractionStatus status)
        => InteractionLifecycle.CanTransition(Status, status);

    /// <summary>
    /// Gets a value indicating whether the interaction's communication session has reached an outcome.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsSettled => InteractionLifecycle.IsSettled(Status);
}
