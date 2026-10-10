using CrestApps.Core;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Realtime;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Settings;

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

        // The tenant's notice that the call is recorded, ahead of the guidance about the opening it belongs to.
        var recordingDisclosure = VoiceCallGuidance.RecordingDisclosure(call?.RecordingDisclosure);

        if (recordingDisclosure is not null)
        {
            orchestration.SystemMessageBuilder.AppendLine();
            orchestration.SystemMessageBuilder.AppendLine(VoiceCallGuidance.RecordingDisclosureHeading);
            orchestration.SystemMessageBuilder.AppendLine();
            orchestration.SystemMessageBuilder.AppendLine(recordingDisclosure);
        }

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
    /// Opens the call's audio stream and the model's session at the same time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The media providers read the site settings, and that read can go through this request's database session --
    /// the same session the orchestrator reads the profile and its prompts through while the model's session opens.
    /// One session cannot serve two reads at once, so the settings are read first, here, and the provider's own read
    /// is then served from what this one loaded. Nothing else the providers do touches the database.
    /// </para>
    /// <para>
    /// Whatever opened is closed again when the other side fails, so a failed call never leaves a stream running at
    /// the provider or a session open at the model.
    /// </para>
    /// </remarks>
    /// <returns>The opened media session, and the model's session or <see langword="null"/> when it could not be started.</returns>
    /// <param name="mediaProvider">The provider carrying the call's audio.</param>
    /// <param name="context">The call being held.</param>
    /// <param name="answeredTicks">When the call was answered, in UTC ticks, for the opening times logged.</param>
    /// <param name="cancellationToken">The call's token.</param>
    private async Task<(IContactCenterVoiceMediaSession Media, IRealtimeConversation Conversation)> OpenMediaAndSessionAsync(
        IContactCenterVoiceMediaProvider mediaProvider,
        RealtimeVoiceConversationContext context,
        long answeredTicks,
        CancellationToken cancellationToken)
    {
        var siteService = ShellScope.Services?.GetService<ISiteService>();

        if (siteService is not null)
        {
            await siteService.GetSiteSettingsAsync();
        }

        var mediaOpening = mediaProvider.OpenSessionAsync(new ContactCenterVoiceMediaSessionRequest
        {
            ProviderCallId = context.ProviderCallId,
            InteractionId = context.InteractionId,
        }, cancellationToken);

        IRealtimeConversation conversation;

        long sessionOpenedTicks = 0;

        try
        {
            conversation = await StartConversationAsync(context, conversationSoFar: null, cancellationToken);
            sessionOpenedTicks = DateTime.UtcNow.Ticks;

            if (conversation is not null)
            {
                await AskForTheGreetingAsync(conversation, cancellationToken);
            }
        }
        catch
        {
            await CloseWhenOpenedAsync(mediaOpening);

            throw;
        }

        try
        {
            var media = await mediaOpening;

            if (conversation is not null && _logger.IsEnabled(LogLevel.Information))
            {
                // What the call runs on, said where its timing is: a call that answers slowly is checked first
                // against the model and the turn detector, and neither was visible anywhere in the log.
                var transport = ShellScope.Services?.GetService<IOptions<RealtimeTransportOptions>>()?.Value;

                _logger.LogInformation(
                    "Opened the call on activity '{ActivityId}' on deployment '{DeploymentName}' (turn detection {TurnDetectionType}, eagerness {TurnDetectionEagerness}): the model's session {SessionMilliseconds} ms and the call's audio stream {MediaMilliseconds} ms after it was answered.",
                    context.Activity?.ItemId.SanitizeLogValue(),
                    context.RealtimeDeploymentName.SanitizeLogValue() ?? "(the realtime slot's)",
                    transport?.TurnDetectionType.SanitizeLogValue() ?? "(default)",
                    transport?.TurnDetectionEagerness.SanitizeLogValue() ?? "(default)",
                    (sessionOpenedTicks - answeredTicks) / TimeSpan.TicksPerMillisecond,
                    (DateTime.UtcNow.Ticks - answeredTicks) / TimeSpan.TicksPerMillisecond);
            }

            return (media, conversation);
        }
        catch
        {
            if (conversation is not null)
            {
                await conversation.DisposeAsync();
            }

            throw;
        }
    }

    /// <summary>
    /// Asks the model for its greeting, on the quick opening detector.
    /// </summary>
    /// <remarks>
    /// <para>
    /// We placed this call, so the silence after the customer picks up is ours to fill. Left to itself the session
    /// waits to be spoken to -- voice detection is how a turn begins -- and every live transcript opened with the
    /// customer saying "Hello?" into dead air before the assistant introduced itself. A session that creates its own
    /// responses ignores this, so it is safe to ask either way.
    /// </para>
    /// <para>
    /// Asked for as soon as the model's session is open, without waiting for the call's audio stream. The model takes
    /// about a second to produce its first audio, and the provider about as long to connect the stream -- live, one
    /// took two and a half -- so asking only once both were open put those two waits end to end. What the model says
    /// before the stream connects waits in the session's events and plays the moment the line is there.
    /// </para>
    /// <para>
    /// Asked for with no instructions of its own, on purpose. Instructions given with one response replace the
    /// session's for that response -- the profile's persona included -- and a call opened that way greeted the
    /// customer as a generic assistant ("Hi there! I'm ChatGPT"). What the opening must be is said in the session's
    /// own instructions instead (see VoiceCallGuidance.WhenTalkedOver).
    /// </para>
    /// </remarks>
    private async Task AskForTheGreetingAsync(IRealtimeConversation conversation, CancellationToken cancellationToken)
    {
        // Quick to hear the caller's first words, then the configured detector: see the Opening partial.
        await ApplyOpeningTurnDetectionAsync(conversation, cancellationToken);
        await conversation.RequestUnpromptedResponseAsync(cancellationToken: cancellationToken);
    }

    // The conversation could not be started, so a stream that opened anyway has nothing to carry.
    private async Task CloseWhenOpenedAsync(Task<IContactCenterVoiceMediaSession> mediaOpening)
    {
        try
        {
            var media = await mediaOpening;
            await media.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "A call's audio stream that was opening alongside a failed realtime session did not open either.");
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
            using var usageScope = AIUsageScope.Begin(contextType: AIUsageCategories.Voice);
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

                    // Kept, so a prompt sent in the middle of the call can carry the session's own instructions with
                    // it (see WithSessionInstructions).
                    _sessionInstructions = orchestration?.SystemMessageBuilder;

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
    /// The instructions for one prompted response: the session's own, followed by what this response is for.
    /// </summary>
    /// <remarks>
    /// Instructions given with a single response replace the session's for that response -- the profile's persona
    /// and every rule about the call included. Sent on their own, "ask whether they are still there" was answered by
    /// a model that no longer knew who it was. So the session's instructions go first, and the prompt is added to
    /// them rather than put in their place.
    /// </remarks>
    /// <param name="prompt">What this response is for.</param>
    private string WithSessionInstructions(string prompt)
    {
        var sessionInstructions = _sessionInstructions?.ToString();

        if (string.IsNullOrWhiteSpace(sessionInstructions))
        {
            return prompt;
        }

        return sessionInstructions.TrimEnd() + "\n\n## Right now\n\n" + prompt;
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
