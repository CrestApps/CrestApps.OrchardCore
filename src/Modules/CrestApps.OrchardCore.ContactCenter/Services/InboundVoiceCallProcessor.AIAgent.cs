using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Hands a call to the AI voice agent its entry point names.
/// </summary>
public sealed partial class InboundVoiceCallProcessor
{
    private const string AIAgentReasonCode = "ai_agent";
    private const string AIAgentUnavailableReasonCode = "ai_agent_unavailable";

    /// <remarks>
    /// The call is recorded as an automated activity with the entry point's AI profile, and answered once the routing
    /// commits so the activity exists when the provider reports the answer. From then on the provider's events for the
    /// call drive the automated voice conversation, as they do for a call the AI places. No Contact Center interaction
    /// is created: the call is nobody's work until the AI hands the caller to a person, and the hand-off creates the
    /// interaction then, as it does for an outbound AI call.
    /// </remarks>
    private async Task<InboundVoiceRoutingResult> RouteToAIAgentAsync(
        InboundVoiceEvent inboundEvent,
        EntryPointRoutingPlan plan,
        OmnichannelChannelEndpoint endpoint,
        SubjectFlowSettings flow,
        string fromAddress,
        string serviceAddress,
        IReadOnlyList<string> contactItemIds,
        DateTime now,
        InboundVoiceRoutingResult result,
        CancellationToken cancellationToken)
    {
        var answerer = _aiVoiceAnswerers.FirstOrDefault(candidate =>
            string.Equals(candidate.ProviderName, inboundEvent.ProviderName, StringComparison.OrdinalIgnoreCase));

        if (answerer is null || string.IsNullOrWhiteSpace(inboundEvent.ProviderCallId))
        {
            // The AI voice feature for this provider was turned off after the entry point was set up. The caller is
            // refused rather than left ringing, and the reason is on the record.
            _logger.LogWarning(
                "Entry point '{EntryPointId}' routes calls to an AI voice agent, but no AI voice agent can answer calls from provider '{ProviderName}'; the call is refused.",
                plan.EntryPoint.ItemId.SanitizeLogValue(),
                inboundEvent.ProviderName.SanitizeLogValue());

            var refused = await CreateActivityAsync(endpoint, flow, fromAddress, contactItemIds, now);
            result.ActivityItemId = refused.ItemId;

            var interaction = await CreateInteractionAsync(inboundEvent, refused, null, fromAddress, serviceAddress, null, null, null, plan.EntryPoint);
            result.InteractionId = interaction.ItemId;
            result.Reason = "The entry point routes to an AI voice agent, which cannot answer calls from this provider.";
            result.ReasonCode = AIAgentUnavailableReasonCode;

            await TerminalizeInboundAsync(
                refused,
                interaction,
                ActivityStatus.Failed,
                InteractionStatus.Failed,
                AIAgentUnavailableReasonCode,
                ProviderCommandType.Reject,
                now,
                cancellationToken);

            return result;
        }

        var activity = await CreateActivityAsync(endpoint, flow, fromAddress, contactItemIds, now, plan.TargetAIProfileId);
        result.ActivityItemId = activity.ItemId;
        result.Routed = true;
        result.Reason = "The call is answered by the entry point's AI voice agent.";
        result.ReasonCode = AIAgentReasonCode;

        var providerCallId = inboundEvent.ProviderCallId;
        var activityId = activity.ItemId;
        var entryPointId = plan.EntryPoint.ItemId;

        _scopeExecutor.ScheduleAfterCommit<IInboundAIVoiceAnswererDispatcher>(dispatcher =>
            dispatcher.AnswerAsync(inboundEvent.ProviderName, providerCallId, activityId, CancellationToken.None));

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Entry point '{EntryPointId}' hands call '{ProviderCallId}' to its AI voice agent on activity '{ActivityId}'.",
                entryPointId.SanitizeLogValue(),
                providerCallId.SanitizeLogValue(),
                activityId.SanitizeLogValue());
        }

        return result;
    }
}
