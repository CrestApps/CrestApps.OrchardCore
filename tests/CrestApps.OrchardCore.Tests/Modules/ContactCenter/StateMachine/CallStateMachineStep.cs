using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.StateMachine;

/// <summary>
/// Represents one generated provider delivery in a randomized call-lifecycle sequence. The step keeps the
/// generated shape separate from the <see cref="ProviderVoiceEvent"/> instance so the same logical delivery
/// can be materialized more than once, which is what duplicate and replay generation relies on.
/// </summary>
public sealed class CallStateMachineStep
{
    /// <summary>
    /// Gets or sets the stable delivery identifier. Two steps that share this identifier are the same
    /// provider delivery and must be suppressed as duplicates.
    /// </summary>
    public string DeliveryId { get; set; }

    /// <summary>
    /// Gets or sets the normalized call state the delivery reports.
    /// </summary>
    public ContactCenterCallState State { get; set; }

    /// <summary>
    /// Gets or sets the provider timestamp the delivery carries.
    /// </summary>
    public DateTime OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the provider sequence number the delivery carries, when the generated provider emits one.
    /// </summary>
    public long? SequenceNumber { get; set; }

    /// <summary>
    /// Gets or sets the mute flag the delivery reports.
    /// </summary>
    public bool? IsMuted { get; set; }

    /// <summary>
    /// Gets or sets the recording state the delivery reports.
    /// </summary>
    public RecordingState? RecordingState { get; set; }

    /// <summary>
    /// Gets or sets the participant count the delivery reports.
    /// </summary>
    public int? ParticipantCount { get; set; }

    /// <summary>
    /// Materializes the provider event for this delivery. Each call produces a new instance carrying the same
    /// idempotency key, so ingesting the result twice exercises the real duplicate-suppression path.
    /// </summary>
    /// <param name="providerName">The provider name to stamp on the event.</param>
    /// <param name="providerCallId">The provider call identifier to stamp on the event.</param>
    /// <returns>The materialized provider voice event.</returns>
    public ProviderVoiceEvent ToProviderEvent(string providerName, string providerCallId)
    {
        return new ProviderVoiceEvent
        {
            ProviderName = providerName,
            ProviderCallId = providerCallId,
            IdempotencyKey = DeliveryId,
            State = State,
            OccurredUtc = OccurredUtc,
            SequenceNumber = SequenceNumber,
            IsMuted = IsMuted,
            RecordingState = RecordingState,
            ParticipantCount = ParticipantCount,
        };
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{DeliveryId}:{State}@{OccurredUtc:HH:mm:ss.fff}#{SequenceNumber?.ToString() ?? "-"}";
    }
}
