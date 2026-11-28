using CrestApps.Core.AI;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Automation;

public sealed partial class AutomatedConversationHandler
{
    // Hands the conversation to a person: the human thread receives the transcript and a short summary, and on success
    // the automated activity concludes as handed off, so the AI never answers again. On failure the activity keeps waiting
    // on the customer, so the conversation is not stranded after the bridge message was sent.
    private async Task PerformHandoffAsync(AutomatedConversationTurn turn, string reason, IOmnichannelHandoffService handoffService, CancellationToken cancellationToken)
    {
        var activity = turn.Activity;
        var conversation = (await GetTranscriptAsync(turn.ChatSession)).ToList();

        // Each turn carries its prompt id and each customer message its provider message id, so the human thread
        // recognises a message it already holds instead of importing it twice.
        var transcript = AutomatedConversationTranscript.Build(conversation);
        var summary = await GenerateHandoffSummaryAsync(turn, conversation, cancellationToken);

        OmnichannelHandoffResult result;

        try
        {
            result = await handoffService.RequestHandoffAsync(new OmnichannelHandoffRequest
            {
                Activity = activity,
                TargetQueueId = turn.FlowSettings.HandoffQueueId,
                Reason = string.IsNullOrWhiteSpace(reason)
                    ? "The automated assistant escalated the conversation to a live agent."
                    : reason,
                Summary = summary,
                ServiceAddress = turn.Endpoint.Value,
                ContactAddress = activity.PreferredDestination,
                Transcript = transcript,
                AiSessionId = turn.ChatSession.SessionId,
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The handoff of automated Activity {ActivityId} threw; the conversation stays with the automated agent.", activity.ItemId.SanitizeLogValue());
            result = OmnichannelHandoffResult.Failure(ex.Message);
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("The handoff of automated Activity {ActivityId} did not complete: {Reason}. The conversation stays with the automated agent.", activity.ItemId.SanitizeLogValue(), result.Message.SanitizeLogValue());

            activity.Status = ActivityStatus.AwaitingCustomerAnswer;

            if (OmnichannelAutomationHelper.HasNoResponseTimeout(turn.FlowSettings))
            {
                activity.ScheduledUtc = OmnichannelAutomationHelper.ResolveNoResponseDeadline(turn.FlowSettings, _clock.UtcNow);
            }

            await _activityStore.UpdateAsync(activity, cancellationToken);

            return;
        }

        // A handoff is its own terminal path: no disposition runs, and the terminal reason lets reports tell escalations
        // from conversations the AI handled alone.
        activity.Status = ActivityStatus.Completed;
        activity.CompletedUtc = _clock.UtcNow;
        activity.TerminalReasonCode = OmnichannelConstants.TerminalReasons.HandedOffToAgent;
        activity.AiEscalated = true;
        activity.CompletedById = activity.AssignedToId;
        activity.CompletedByUsername = activity.AssignedToUsername;
        ActivityDispositionActors.Stamp(activity, ActivityDispositionActor.AIAgent);

        if (string.IsNullOrWhiteSpace(activity.Notes))
        {
            activity.Notes = $"The automated {turn.Channel.ConversationNoun} was handed off to a live agent.";
        }

        await _activityStore.UpdateAsync(activity, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Handed automated {Channel} Activity {ActivityId} off to a live agent (conversation {ConversationId}).", turn.Channel.Channel, activity.ItemId.SanitizeLogValue(), (result.ConversationId ?? "(none)").SanitizeLogValue());
        }
    }

    // A short plain-text summary for the agent taking over. Best effort: a failure returns nothing and the handoff goes on.
    private async Task<string> GenerateHandoffSummaryAsync(AutomatedConversationTurn turn, IReadOnlyList<AIChatSessionPrompt> conversation, CancellationToken cancellationToken)
    {
        var prompt =
            $"You are handing this {turn.Channel.ConversationNoun} to a human agent. In 2-3 short sentences of plain text (no " +
            "preamble, labels, or quotes), summarize for the agent what the customer wants, the key facts they shared, and why " +
            "they are being transferred.";

        try
        {
            var context = await _completionContextBuilder.BuildAsync(turn.Profile, builder =>
            {
                builder.SystemMessage = prompt;
                builder.DisableTools = true;
            }, cancellationToken);

            var deployment = await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName: context.ChatDeploymentName, cancellationToken: cancellationToken);

            if (deployment is null)
            {
                return null;
            }

            var client = await _aiClientFactory.CreateChatClientAsync(deployment, builder => builder.UseDefaultResilience().UseUsageLabels(contextType: turn.Channel.UsageCategory, purpose: AIUsageFeaturePurposes.HandoffSummary));

            var messages = new List<ChatMessage> { new(ChatRole.System, prompt) };
            messages.AddRange(conversation.Select(entry => new ChatMessage(entry.Role, entry.Content)));

            var response = await client.GetResponseAsync(messages, cancellationToken: cancellationToken);

            return response?.Text?.Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Generating the handoff summary of automated Activity {ActivityId} failed; continuing without one.", turn.Activity.ItemId.SanitizeLogValue());

            return null;
        }
    }
}
