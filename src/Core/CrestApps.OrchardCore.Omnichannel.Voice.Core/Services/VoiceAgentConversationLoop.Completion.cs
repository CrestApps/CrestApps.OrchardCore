using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Guardrails;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Liquid;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// One turn of a turn-based call: the model's reply to what the caller said, with the call's tools and the
/// supervisor's notes.
/// </summary>
public sealed partial class VoiceAgentConversationLoop
{
    private async Task<(string Reply, bool HandoffRequested, string Reason, bool EndCallRequested)> CompleteAsync(AIProfile profile, AIChatSession session, OmnichannelActivity activity, string turnGuidance, CancellationToken cancellationToken)
    {
        var prompts = await _promptStore.GetPromptsAsync(session.SessionId);

        // A caller line the profile's guardrails refuse is answered with its placeholder reply, without asking the
        // model, so a refused request never reaches the tools either.
        if (string.IsNullOrEmpty(turnGuidance) && _guardrails is not null && AIGuardrailService.IsOn(profile, AIGuardrailDirection.Customer))
        {
            var callerLine = prompts.LastOrDefault(prompt => prompt.Role == ChatRole.User && !prompt.IsGeneratedPrompt)?.Content;
            var refused = await _guardrails.CheckAsync(profile, callerLine, AIGuardrailDirection.Customer, session.SessionId, cancellationToken);

            if (refused is not null)
            {
                return (refused.Reply, false, null, false);
            }
        }

        var transcript = prompts
            .Where(p => !p.IsGeneratedPrompt)
            .Select(p => new ChatMessage(p.Role, (p.Content ?? string.Empty).Replace(HangupMarker, string.Empty)))
            .ToList();

        // When a live agent is available, enable the transfer tool for this turn and guide the model on when to
        // escalate. Guidance is injected as a leading system message so the persona system prompt is preserved.
        var (handoffService, flowSettings) = await ResolveVoiceHandoffAsync(activity, cancellationToken);

        if (handoffService is not null)
        {
            var handoffInstructions = OmnichannelHandoffHelper.BuildHandoffInstructions(flowSettings);

            if (!string.IsNullOrEmpty(handoffInstructions))
            {
                transcript.Insert(0, new ChatMessage(ChatRole.System, handoffInstructions));
            }
        }

        // The same thing the live session is told, because a turn-based call is the same call: somebody has to
        // hang up, and the model is the only one here who knows the conversation is over.
        transcript.Insert(0, new ChatMessage(ChatRole.System, VoiceCallGuidance.PhoneMenusAndScreening));
        transcript.Insert(0, new ChatMessage(ChatRole.System, VoiceCallGuidance.EndingTheCall));

        // What this particular turn is for, when it is not a reply to the customer. Put to the model as the turn it
        // answers: added as one more system note it was outweighed by the persona and the conversation, and the
        // model carried on selling to the recording instead of leaving a message on it.
        if (!string.IsNullOrEmpty(turnGuidance))
        {
            transcript.Add(new ChatMessage(ChatRole.User, turnGuidance));
        }

        // Notes a supervisor sent while the caller was talking, read once on this turn.
        foreach (var note in await TakeSupervisorNotesAsync(activity, cancellationToken))
        {
            transcript.Add(new ChatMessage(ChatRole.System, SupervisorNoteGuidance(note)));
        }

        // The conversation this turn belongs to, current while the request is built (the system message is rendered
        // with its values) and while the model's tools run (they read the caller, the contact and the variables).
        var conversation = await _conversationContextFactory.CreateAsync(activity, session, profile, cancellationToken);
        using var conversationScope = AIConversationScope.Begin(conversation);
        using var invocationScope = AIInvocationScope.Begin();
        invocationScope.Context.ToolExecutionContext = new AIToolExecutionContext(profile);
        invocationScope.Context.ChatSession = session;

        var context = await _contextBuilder.BuildAsync(profile, cancellationToken: cancellationToken);
        context.AdditionalProperties["Session"] = session;
        invocationScope.Context.CompletionContext = context;

        // Attach the profile's tools and the call's own once the context exists. The automated conversation calls
        // the completion service directly rather than through the tool orchestrator, so the tools must be added here
        // or the model never receives them. The SMS handler does the same.
        await AttachCallToolsAsync(context, offerTransfer: handoffService is not null, cancellationToken);

        // A slow tool must not leave the caller in silence: each tool has a time limit, and the caller hears a short
        // filler line when one runs long.
        using var toolFiller = await LimitCallToolsAsync(context, activity, cancellationToken);

        var deployment = await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName: context.ChatDeploymentName, cancellationToken: cancellationToken);

        if (deployment is null)
        {
            return (null, false, null, false);
        }

        // The completion auto-invokes these tools when the model decides to escalate or to finish; each records
        // its decision on a scoped turn, which is reset first and read back once the completion returns.
        _handoffTurn.Reset();
        _endCallTurn.Reset();

        using var usageScope = AIUsageScope.Begin(contextType: AIUsageCategories.Voice);
        var completion = await _completionService.CompleteAsync(deployment, transcript, context, cancellationToken);

        var reply = completion?.Messages?.FirstOrDefault()?.Text;
        var handoffRequested = handoffService is not null && _handoffTurn.HandoffRequested;

        // A reply the guardrails refuse is replaced before it is stored or spoken. What the model decided to do
        // (hand over, hang up) stands; only its words change.
        if (!string.IsNullOrWhiteSpace(reply) && _guardrails is not null && AIGuardrailService.IsOn(profile, AIGuardrailDirection.Reply))
        {
            var refused = await _guardrails.CheckAsync(profile, reply.Replace(HangupMarker, string.Empty, StringComparison.Ordinal), AIGuardrailDirection.Reply, session.SessionId, cancellationToken);

            if (refused is not null)
            {
                reply = refused.Reply;
            }
        }

        // A call being handed to a person is not a call that is over, whatever the model asked for alongside it.
        var endCallRequested = _endCallTurn.EndCallRequested && !handoffRequested;

        return (reply, handoffRequested, _handoffTurn.Reason, endCallRequested);
    }
}
