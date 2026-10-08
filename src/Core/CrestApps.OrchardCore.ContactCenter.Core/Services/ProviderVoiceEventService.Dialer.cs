using System.Globalization;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// How a dialer attempt's end is read: whether an agent was ever connected to it, and what its outcome was.
/// </summary>
public sealed partial class ProviderVoiceEventService
{
    /// <summary>
    /// Whether the call's provider connects the agent through a leg of its own once the customer answers, rather than
    /// having the agent on the call from the start.
    /// </summary>
    private bool ProviderJoinsAgentAfterAnswer(CallSession session)
    {
        var provider = _voiceProviderResolver.Get(session.ProviderName);

        return provider is not null &&
            provider.DeliveryModel == VoiceProviderDeliveryModel.ServerSideAcd &&
            provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.AgentConnect) &&
            provider is IContactCenterVoiceCallControlProvider;
    }

    /// <summary>
    /// Records the outcome of an outbound activity call that just ended.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the call was a campaign dial that no agent was ever connected to, so it is not the
    /// agent's call to wrap up.
    /// </returns>
    private bool RecordDialerAttemptEnd(CallSession session, Interaction interaction, DateTime now)
    {
        if (session.Direction != InteractionDirection.Outbound || string.IsNullOrEmpty(interaction.ActivityItemId))
        {
            return false;
        }

        var answered = session.AnsweredUtc.HasValue || interaction.AnsweredUtc.HasValue;
        var isCampaignDial = DialerCallMetadata.IsCampaignDial(interaction);

        // The agent's leg answering is what marks the agent joined; the leg's own record on the topology says the
        // same and is read too, so a mark lost to a race between the two writes does not turn a real conversation into
        // a dropped call. A provider that has the agent on the call from the start joined them when it answered.
        var agentJoined = DialerCallMetadata.HasAgentJoined(interaction) ||
            session.Legs.Any(leg => leg is not null && leg.Role == CallPartyRole.Agent && leg.AnsweredUtc.HasValue) ||
            (answered && !ProviderJoinsAgentAfterAnswer(session));

        if (agentJoined && isCampaignDial)
        {
            DialerCallMetadata.MarkAgentJoined(interaction, session.AnsweredUtc ?? interaction.AnsweredUtc ?? now);
        }

        var outcome = DialerAttemptOutcomes.Resolve(session.HangupCause, answered, agentJoined || !isCampaignDial);

        DialerCallMetadata.SetOutcome(interaction, outcome);

        var endedWithoutAgent = isCampaignDial && !agentJoined;

        if (endedWithoutAgent && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Dialer call '{ProviderCallId}' (interaction '{InteractionId}', activity '{ActivityId}') ended as {Outcome} before an agent was connected; the agent is released without wrap-up and the dialer dispositions the attempt. HangupCause={HangupCause}, Answered={Answered}.",
                session.ProviderCallId.SanitizeLogValue(),
                interaction.ItemId.SanitizeLogValue(),
                interaction.ActivityItemId.SanitizeLogValue(),
                outcome,
                session.HangupCause,
                answered);
        }

        return endedWithoutAgent;
    }

    private static void AddDialerAttemptDetails(CallLifecycleEventData data, Interaction interaction)
    {
        var profileId = DialerCallMetadata.GetDialerProfileId(interaction);

        if (!string.IsNullOrEmpty(profileId))
        {
            data.Details["dialerProfileId"] = profileId;
        }

        if (DialerCallMetadata.GetAttemptNumber(interaction) is { } attemptNumber)
        {
            data.Details["attemptNumber"] = attemptNumber.ToString(CultureInfo.InvariantCulture);
        }

        if (DialerCallMetadata.GetMaxAttempts(interaction) is { } maxAttempts)
        {
            data.Details["maxAttempts"] = maxAttempts.ToString(CultureInfo.InvariantCulture);
        }

        data.Details["agentJoined"] = DialerCallMetadata.HasAgentJoined(interaction) ? bool.TrueString : bool.FalseString;
    }
}
