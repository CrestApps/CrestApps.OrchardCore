using System.Data.Common;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Automation;

public sealed partial class AutomatedConversationHandler
{
    // Before each reply the agent judges whether a reply is warranted at all, the way a person glances at the thread and
    // sometimes sends nothing: a bare "thanks" after the conversation wrapped up, or a repeat that adds nothing.
    private static string BuildShouldRespondPrompt(string conversationNoun)
        => $"""
        You are the agent in an ongoing {conversationNoun} with a customer. Read the whole conversation, then decide whether
        the agent should send a NEW reply to the customer's most recent message right now.
        Set ShouldRespond to true whenever the customer answered a question you asked or gave new information, including a
        short confirmation such as "yes" or "no" in response to your question, because a person would acknowledge it and
        then either continue or gracefully close the conversation.
        Set ShouldRespond to false only when a thoughtful human agent would genuinely send nothing: the latest message is a
        bare acknowledgement that needs no follow-up (such as "ok" or "thanks") after you have already wrapped up; or it
        merely repeats a point you already asked about or answered and adds no new information; or the conversation has
        clearly ended. When you are unsure, prefer to respond. Always include a short Reason.
        """;

    // Returns true to reply, false when no reply is warranted, and null when the judgement could not be made: no chat
    // deployment, a failed call, or an answer that could not be read. The caller fails closed on null and sends nothing,
    // so a broken judgement never lets the agent answer every message, including an automated mailbox's.
    private async Task<bool?> ShouldRespondAsync(AutomatedConversationTurn turn, List<ChatMessage> transcript, CancellationToken cancellationToken)
    {
        var prompt = BuildShouldRespondPrompt(turn.Channel.ConversationNoun);

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
                _logger.LogWarning("No chat deployment is available to judge whether to reply in Activity {ActivityId}.", turn.Activity.ItemId.SanitizeLogValue());

                return null;
            }

            var client = await _aiClientFactory.CreateChatClientAsync(deployment, builder => builder.UseDefaultResilience().UseUsageLabels(contextType: turn.Channel.UsageCategory, purpose: AIUsageFeaturePurposes.ReplyDecision));

            var messages = new List<ChatMessage> { new(ChatRole.System, prompt) };
            messages.AddRange(transcript);

            var decision = await client.GetResponseAsync<ShouldRespondResult>(messages, _jsonSerializerOptions.SerializerOptions, cancellationToken: cancellationToken);

            if (decision.Result is null)
            {
                _logger.LogWarning("The should-respond evaluation for Activity {ActivityId} returned no decision.", turn.Activity.ItemId.SanitizeLogValue());

                return null;
            }

            if (!decision.Result.ShouldRespond)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Skipping an automated reply for Activity {ActivityId}: {Reason}", turn.Activity.ItemId.SanitizeLogValue(), decision.Result.Reason.SanitizeLogValue());
                }

                return false;
            }

            return true;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is not DbException)
        {
            // Fail closed: without a decision the agent does not reply blindly. A database failure is not swallowed, since
            // it cancels the session and a recovery pass must stop rather than send replies it can no longer record.
            _logger.LogWarning(ex, "The should-respond evaluation failed for Activity {ActivityId}; no reply is sent.", turn.Activity.ItemId.SanitizeLogValue());

            return null;
        }
    }

    private sealed class ShouldRespondResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the agent should reply to the customer's latest message now.
        /// </summary>
        public bool ShouldRespond { get; set; }

        /// <summary>
        /// Gets or sets a short reason for the decision.
        /// </summary>
        public string Reason { get; set; }
    }
}
