using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Settles an agent's call-history entry for a call they were offered and never took, the moment the offer ends.
/// </summary>
/// <remarks>
/// The soft-phone projection records a call in the agent's history as soon as it rings them, and afterwards follows
/// whichever agent the call is with. An offer that expired, was declined or was withdrawn left its agent's entry "in
/// progress": the projection had moved on to the next agent, and the call's end reached only one entry for the call.
/// Until the call ended somewhere else and the telephony sweep noticed, the agent's history showed a live call they
/// had never answered, and the soft phone's active-call lookup handed it back to their phone as one.
/// </remarks>
public sealed class OfferCallHistorySettlementHandler : IContactCenterEventHandler
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly ITelephonyInteractionStore _telephonyInteractionStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="OfferCallHistorySettlementHandler"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager, read for the offered call.</param>
    /// <param name="callSessionManager">The call-session manager, read for the call's provider identity.</param>
    /// <param name="agentProfileManager">The agent profile manager, read for the offered agent's user.</param>
    /// <param name="telephonyInteractionStore">The store that holds the agent's call history.</param>
    public OfferCallHistorySettlementHandler(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IAgentProfileManager agentProfileManager,
        ITelephonyInteractionStore telephonyInteractionStore)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _agentProfileManager = agentProfileManager;
        _telephonyInteractionStore = telephonyInteractionStore;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/OfferCallHistorySettlement/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (ResolveOutcome(interactionEvent.EventType) is not CallOutcome outcome)
        {
            return;
        }

        var offer = interactionEvent.GetData<OfferLifecycleEventData>();
        var interactionId = offer?.InteractionId ?? interactionEvent.InteractionId;

        if (offer is null || string.IsNullOrEmpty(interactionId) || string.IsNullOrEmpty(offer.AgentId))
        {
            return;
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null)
        {
            return;
        }

        // Delivered after the call came back to this agent -- offered to them again, or taken by them -- the entry
        // belongs to the call they have now, and the projection is keeping it.
        if (!interaction.IsSettled &&
            string.Equals(interaction.AgentId, offer.AgentId, StringComparison.Ordinal))
        {
            return;
        }

        var userId = offer.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            userId = (await _agentProfileManager.FindByIdAsync(offer.AgentId, cancellationToken))?.UserId;
        }

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // The entry is keyed the way the projection wrote it: by the session's provider call, else the interaction's.
        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);
        var callId = session?.ProviderCallId ?? interaction.ProviderInteractionId;
        var entry = await _telephonyInteractionStore.FindByCallIdAsync(userId, callId, cancellationToken);

        if ((entry is null || entry.Outcome != CallOutcome.InProgress) &&
            !string.Equals(callId, interaction.ProviderInteractionId, StringComparison.Ordinal))
        {
            entry = await _telephonyInteractionStore.FindByCallIdAsync(userId, interaction.ProviderInteractionId, cancellationToken);
        }

        // Settled already -- the call ended, or a voicemail was recorded for this agent -- is settled for good.
        if (entry is null || entry.Outcome != CallOutcome.InProgress)
        {
            return;
        }

        var endedUtc = offer.SettledUtc ?? interactionEvent.OccurredUtc;

        entry.Outcome = outcome;
        entry.AwaitingAnswer = false;
        entry.EndedUtc = endedUtc;
        entry.DurationSeconds = entry.StartedUtc == default
            ? 0
            : Math.Max(0, (endedUtc - entry.StartedUtc).TotalSeconds);

        await _telephonyInteractionStore.UpdateAsync(entry, cancellationToken);
    }

    // A declined offer was turned down; every other offer that ended without an accept is one the agent missed.
    private static CallOutcome? ResolveOutcome(string eventType)
        => eventType switch
        {
            ContactCenterConstants.Events.OfferDeclined => CallOutcome.Rejected,
            ContactCenterConstants.Events.OfferExpired or
                ContactCenterConstants.Events.OfferMissed or
                ContactCenterConstants.Events.OfferCancelled => CallOutcome.Missed,
            _ => null,
        };
}
