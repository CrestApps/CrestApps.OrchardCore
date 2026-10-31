using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What a turn-based call does when nobody speaks.
/// </summary>
/// <remarks>
/// Split from the loop because it is reached from the silence watchdog rather than from a provider event, and
/// because the loop's file is at the size the architecture guard allows.
/// </remarks>
public sealed partial class VoiceAgentConversationLoop
{
    /// <summary>
    /// What the assistant says when the line has gone quiet.
    /// </summary>
    /// <remarks>
    /// Fixed rather than generated, and short. It is also how a silent stretch is recognised in the transcript, so
    /// the call can tell the second silence from the first without keeping any state of its own.
    /// </remarks>
    internal const string StillThereLine = "Sorry, are you still there?";

    /// <summary>
    /// What the assistant says before it hangs up on a line that stayed silent.
    /// </summary>
    internal const string SilentLineGoodbye = "It sounds like now isn't a good time, so I'll let you go. Goodbye.";

    /// <summary>
    /// Why a call was ended after the line stayed quiet through every prompt.
    /// </summary>
    internal const string SilentLineEndedReason = "The line stayed silent after the assistant asked whether anybody was there.";

    /// <summary>
    /// How many times a silence is broken before the call is ended. The same limit a live session keeps.
    /// </summary>
    private const int MaximumStillTherePrompts = 2;

    /// <summary>
    /// Called once a listening turn has passed with nobody speaking.
    /// </summary>
    /// <remarks>
    /// Asks whether the caller is still there, twice, and then says goodbye and ends the call through the same
    /// speak-then-hang-up path a goodbye from the model takes. On a call this platform placed, a line nobody has
    /// spoken on at all is taken to be a voicemail that is recording, and is left a message instead. A turn the watch
    /// no longer describes -- the caller spoke, the assistant replied, the call ended or was handed to an agent -- is
    /// left alone.
    /// </remarks>
    /// <param name="silence">The listening turn, as it stood when listening began.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task OnListeningTimedOutAsync(TurnBasedSilence silence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(silence);

        var activity = await _activityStore.FindByIdAsync(silence.ActivityId, cancellationToken);

        if (activity is null ||
            activity.Status.IsTerminal() ||
            activity.AiEscalated ||
            activity.TryGet<PendingVoiceHandoff>(out _) ||
            activity.TryGet<AIAttendedTransfer>(out _))
        {
            return;
        }

        var (profile, session) = await ResolveConversationAsync(activity, cancellationToken);

        if (profile is null || session is null)
        {
            return;
        }

        var prompts = await _promptStore.GetPromptsAsync(session.SessionId);

        // Something has been said since listening began, so this silence is over.
        if (prompts.Count != silence.PromptCount)
        {
            return;
        }

        var media = _mediaResolver.Get(silence.ProviderName) ?? _mediaResolver.GetDefault();

        if (media is null)
        {
            return;
        }

        // Nobody has said a word since the call was answered. A person who picks up speaks; a line that stays silent
        // through the whole opening line is a voicemail whose greeting played underneath it -- the assistant only
        // listens once it has finished speaking, so a short greeting is never heard. Live, such a call was asked
        // "are you still there?" twice and told "now isn't a good time", all of it recorded as the message.
        var nobodyHasSpoken = !prompts.Any(prompt =>
            prompt.Role == ChatRole.User &&
            !prompt.IsGeneratedPrompt &&
            !string.IsNullOrWhiteSpace(prompt.Content));

        // Only a call this platform placed can reach a voicemail. Somebody who called in is a person, and a silence
        // from them is a caller who has not spoken yet. Live, an inbound caller who said nothing at first was told
        // "Sorry we missed you. We were calling about your recent inquiry" and hung up on.
        var weCalled = !string.Equals(activity.Source, ActivitySources.Inbound, StringComparison.Ordinal);

        if (!activity.TryGet<VoicemailReached>(out var voicemail) && nobodyHasSpoken && weCalled)
        {
            voicemail = new VoicemailReached();
            activity.Put(voicemail);
            await _activityStore.UpdateAsync(activity, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Nobody has spoken on automated call activity '{ActivityId}' since it was answered, so it is taken to be a voicemail and a message is being left.",
                    activity.ItemId.SanitizeLogValue());
            }
        }

        // A voicemail has gone quiet because its greeting is over and it is recording: that is the moment to leave
        // the message, not to ask a recording whether it is still there.
        if (voicemail is not null)
        {
            await media.StopTranscriptionAsync(silence.ProviderCallId, cancellationToken);

            if (!voicemail.MessageLeft)
            {
                await LeaveVoicemailAsync(media, silence.ProviderCallId, activity, voicemail, profile, session, cancellationToken);
            }

            return;
        }

        var stillTherePrompts = Enumerable.Reverse(prompts)
            .TakeWhile(prompt => prompt.Role == ChatRole.Assistant)
            .Count(prompt => string.Equals(prompt.Content?.Trim(), StillThereLine, StringComparison.Ordinal));

        // Stop listening before speaking, exactly as a reply does, so the assistant's own voice is not transcribed.
        await media.StopTranscriptionAsync(silence.ProviderCallId, cancellationToken);

        if (stillTherePrompts < MaximumStillTherePrompts)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Nobody has spoken on automated call activity '{ActivityId}', so the assistant is asking whether the caller is still there (attempt {Attempt}).",
                    activity.ItemId.SanitizeLogValue(),
                    stillTherePrompts + 1);
            }

            await StorePromptAsync(session, ChatRole.Assistant, StillThereLine, cancellationToken);
            await SpeakAsync(media, silence.ProviderCallId, activity, StillThereLine, cancellationToken);

            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Nobody has spoken on automated call activity '{ActivityId}' after {Attempts} prompts, so the call is being ended.",
                activity.ItemId.SanitizeLogValue(),
                stillTherePrompts);
        }

        // The marker is what the speak.ended handler hangs up on, once the goodbye has been heard in full.
        await StorePromptAsync(session, ChatRole.Assistant, SilentLineGoodbye + " " + HangupMarker, cancellationToken);
        await SpeakAsync(media, silence.ProviderCallId, activity, SilentLineGoodbye, cancellationToken);
    }

    /// <summary>
    /// Whether the caller said something new while the reply to their last line was being written.
    /// </summary>
    /// <remarks>
    /// The newer line is answered by its own turn, which sees everything said before it. Speaking this reply as well
    /// queued one answer behind another: live, a caller who pressed five keys heard five replies in a row, and what
    /// they said in between was answered after those, so the assistant talked over itself for half a minute.
    /// </remarks>
    /// <param name="voiceEvent">The caller's line this reply answers.</param>
    /// <param name="heard">How many caller lines the call had heard once that line arrived.</param>
    private bool ReplyWasOvertaken(VoiceAgentEvent voiceEvent, long heard)
    {
        if (_silenceWatchdog.HeardCount(voiceEvent.ActivityId) == heard)
        {
            return false;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The caller on automated call activity '{ActivityId}' said something new while the reply to their last line was being written, so that reply is left unsaid.",
                voiceEvent.ActivityId.SanitizeLogValue());
        }

        return true;
    }

    private Task WatchForSilenceAsync(VoiceAgentEvent voiceEvent, int promptCount, TimeSpan? wait = null)
        => _silenceWatchdog.ArmAsync(new TurnBasedSilence
        {
            ActivityId = voiceEvent.ActivityId,
            ProviderName = voiceEvent.ProviderName,
            ProviderCallId = voiceEvent.ProviderCallId,
            PromptCount = promptCount,
            Wait = wait,
        });
}
