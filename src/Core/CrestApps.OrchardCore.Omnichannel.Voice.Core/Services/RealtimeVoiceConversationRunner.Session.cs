using CrestApps.Core;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Realtime;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Opening the live session a call is held on, and what it is told when it opens.
/// </summary>
/// <remarks>
/// Split from the session itself because a call can now open more than one: a session lost partway through is
/// replaced, and the replacement is built exactly the way the first was, plus the conversation so far.
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// Gives the live session the tools a phone call needs, and tells it when to use them.
    /// </summary>
    /// <remarks>
    /// A realtime session is configured once, at the start, from this context — there is no per-turn completion
    /// to hang a tool on the way the turn-based path does. The end-call tool is registered as a scoped system
    /// entry and named in <c>MustIncludeTools</c> so the profile's own tool selection cannot leave it out: every
    /// call has to be endable, whatever else the profile is configured to do.
    /// </remarks>
    /// <param name="orchestration">The orchestration context the session is built from.</param>
    /// <param name="call">The call being held.</param>
    private static void ConfigureCallTools(OrchestrationContext orchestration, RealtimeVoiceConversationContext call)
    {
        if (orchestration?.CompletionContext is null)
        {
            return;
        }

        AttachTool(orchestration, EndCallTool.ToolName, "Ends the phone call once the conversation is over.");

        // Escalation, when this call has somewhere to escalate to. The tool and the guidance travel together:
        // a tool the model is never told about is not used, and guidance without a tool has the model promising
        // a transfer that nothing performs.
        if (!string.IsNullOrWhiteSpace(call?.HandoffInstructions))
        {
            AttachTool(
                orchestration,
                OmnichannelHandoffHelper.TransferToAgentToolName,
                "Transfers the current conversation to a live human agent.");

            orchestration.SystemMessageBuilder.AppendLine();
            orchestration.SystemMessageBuilder.AppendLine(call.HandoffInstructions);
        }

        // Somebody trying to end contact must never be handed to a person instead. This is written for the way it
        // actually arrives: speech recognition on a phone line drops small words, and "don't call me" reaches the
        // model as "call me" — which reads as a request to be connected and was, on a live call, acted on as one.
        // The safe reading of an ambiguous fragment near a refusal is the one that stops calling.
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine("## When somebody asks you to stop calling");
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine(
            "If the customer asks not to be called, to be taken off the list, or to stop calling, that is an " +
            "opt-out and it ends the call. Acknowledge it plainly, say they will not be contacted again, and end " +
            "the call. Never transfer somebody who is trying to end contact, and never treat it as interest. " +
            "Phone audio drops small words, so a short or garbled phrase around a refusal — including one that " +
            "sounds like an invitation to call — is an opt-out unless the customer clearly says otherwise; if you " +
            "genuinely cannot tell, ask them to confirm rather than assuming the answer that keeps them on the list.");

        // Who picked up. A model opening a sales call with no name does not decline to use one — it invents a
        // plausible one, and the person who answers knows immediately that nobody actually knows them. Observed
        // live: "is this Marcus?" to a contact named Amani, who asked who it was looking for, which the assistant
        // then read as a request for a human and transferred the call.
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine("## Who you are calling");
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine(
            string.IsNullOrWhiteSpace(call?.ContactName)
                ? "You do not know the name of the person you are calling. Do not use a name, and never guess or " +
                  "invent one; ask who you are speaking with if you need it."
                : $"You are calling {call.ContactName}. That is the only name you may use for them. Never use any " +
                  "other name, and never guess or invent one — if the person says they are somebody else, believe " +
                  "them and adjust.");

        // Being talked over cuts the model's own line back to what the caller heard, so an interrupted opening is
        // a word long in its record — and a profile that says "open by introducing yourself" then has it start
        // the introduction again. Live, four times in fifteen seconds. See VoiceCallGuidance.WhenTalkedOver.
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine(VoiceCallGuidance.WhenTalkedOverHeading);
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine(VoiceCallGuidance.WhenTalkedOver);

        // Said plainly, because the model is speaking rather than writing and cannot see the call state: on a
        // phone call somebody has to hang up, and if it does not, the customer is left holding a dead line.
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine("## Ending the call");
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine(VoiceCallGuidance.EndingTheCall);
    }

    /// <summary>
    /// Puts one system tool in front of a realtime session.
    /// </summary>
    /// <remarks>
    /// Registered as a scoped entry (the profile's own tool list skips system tools) and named in
    /// <c>MustIncludeTools</c> so the profile's selection cannot leave it out. Both are idempotent, because this
    /// runs once per call and a duplicate would be offered to the model twice.
    /// </remarks>
    /// <param name="orchestration">The orchestration context the session is built from.</param>
    /// <param name="toolName">The tool's registered name.</param>
    /// <param name="description">What the tool does, for the registry entry.</param>
    private static void AttachTool(OrchestrationContext orchestration, string toolName, string description)
    {
        var scoped = orchestration.CompletionContext.AdditionalProperties
            .TryGetValue(FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey, out var existing) &&
            existing is List<ToolRegistryEntry> entries
                ? entries
                : [];

        if (!scoped.Exists(entry => entry.Id == toolName))
        {
            scoped.Add(new ToolRegistryEntry
            {
                Id = toolName,
                Name = toolName,
                Description = description,
                Source = ToolRegistryEntrySource.System,
                CreateAsync = serviceProvider => ValueTask.FromResult(
                    serviceProvider.GetKeyedService<AITool>(toolName)),
            });
        }

        orchestration.CompletionContext.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey] = scoped;

        if (!orchestration.MustIncludeTools.Contains(toolName))
        {
            orchestration.MustIncludeTools.Add(toolName);
        }
    }

    /// <summary>
    /// Opens a live session for the call, or returns <see langword="null"/> when one cannot be opened.
    /// </summary>
    /// <param name="context">The call being held.</param>
    /// <param name="conversationSoFar">
    /// What has been said on the call already, for a session that replaces one that was lost; <see langword="null"/>
    /// for the call's first session.
    /// </param>
    /// <param name="cancellationToken">The call's token.</param>
    private async Task<IRealtimeConversation> StartConversationAsync(
        RealtimeVoiceConversationContext context,
        string conversationSoFar,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _orchestrator.StartAsync(new RealtimeOrchestrationRequest
            {
                Resource = context.Profile,
                RealtimeDeploymentName = context.RealtimeDeploymentName,
                ChatSession = context.Session,

                // The campaign's voice when the inventory load chose one, otherwise the voice configured on the
                // profile itself. Only the activity was read before, and a batch does not set a voice unless
                // somebody picks one — so the voice an operator selected on the profile was silently ignored and
                // every realtime call used the model's default, whatever the profile said.
                Voice = ResolveVoice(context),

                // A caller who talks over the assistant is interrupting a person as far as they are concerned,
                // and being talked through is the single most common complaint about automated calls.
                AllowInterruption = true,

                // A realtime session is built from this context rather than from a per-turn completion, so the
                // tools a live call needs — and the instruction to use them — have to be put here. Without it the
                // model has no way to end a call it knows is over. A replacement session starts with no history
                // at all, so what has been said is put here too, or it would open the call all over again.
                ConfigureContext = orchestration =>
                {
                    ConfigureCallTools(orchestration, context);

                    if (!string.IsNullOrEmpty(conversationSoFar) && orchestration?.SystemMessageBuilder is not null)
                    {
                        orchestration.SystemMessageBuilder.AppendLine();
                        orchestration.SystemMessageBuilder.AppendLine(conversationSoFar);
                    }
                },
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "A realtime voice session could not be started for activity '{ActivityId}'.", context.Activity?.ItemId.SanitizeLogValue());

            return null;
        }
    }

    /// <summary>
    /// The voice this call should be spoken in: the one the activity was loaded with, falling back to the one
    /// configured on the profile.
    /// </summary>
    /// <remarks>
    /// The activity only carries a voice when the inventory load explicitly chose one, which is the exception
    /// rather than the rule. Reading only the activity therefore threw away the profile's own setting, so an
    /// operator who picked a voice there heard the model's default on every call and had no way to tell why.
    /// </remarks>
    /// <param name="context">The call being held.</param>
    private static string ResolveVoice(RealtimeVoiceConversationContext context)
    {
        var activityVoice = context.Activity?.TextToSpeechVoiceId;

        if (!string.IsNullOrWhiteSpace(activityVoice))
        {
            return activityVoice.Trim();
        }

        if (context.Profile is not null &&
            context.Profile.TryGetSettings<ChatModeProfileSettings>(out var settings) &&
            !string.IsNullOrWhiteSpace(settings.VoiceName))
        {
            return settings.VoiceName.Trim();
        }

        // Nothing chosen anywhere: let the deployment use whatever it defaults to.
        return null;
    }
}
