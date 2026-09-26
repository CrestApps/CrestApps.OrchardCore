using System.Collections.Concurrent;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What a turn-based call does when it is answered by voicemail or an answering machine.
/// </summary>
/// <remarks>
/// A recording is heard exactly like somebody answering, so without this the greeting was replied to as if it were
/// the customer. Once a greeting is recognised the call stops conversing: it lets the greeting finish, leaves one
/// short message, and hangs up, and it is concluded as a call nobody answered.
/// </remarks>
public sealed partial class VoiceAgentConversationLoop
{
    /// <summary>
    /// How long a greeting that has not yet asked for the message is given to finish before the message is left.
    /// </summary>
    /// <remarks>
    /// The tone follows the greeting, and everything after it is recorded, so this is kept to a few seconds: long
    /// enough for the rest of the greeting and the tone, short enough that the recording is not mostly silence.
    /// </remarks>
    internal static readonly TimeSpan VoicemailToneWait = TimeSpan.FromSeconds(3);

    /// <summary>
    /// What the model is told when the time has come to leave the message.
    /// </summary>
    internal const string LeavingAVoicemail =
        "[Not the customer] This call went to voicemail and it is now recording; any customer lines before this are " +
        "its recorded greeting. Reply with only the voicemail message to leave, in one to three short sentences: greet the " +
        "customer by name if you know it, say who you are and why you called, and say you will try them again. It is " +
        "a recording, so do not ask any questions.";

    /// <summary>
    /// The message left when the model produces none.
    /// </summary>
    internal const string FallbackVoicemailMessage =
        "Hi, sorry we missed you. We were calling about your recent inquiry and will try you again soon. Have a great day.";

    // The calls whose message is being composed right now. Composing takes seconds, and during them a late
    // transcript of the rest of the greeting, the silence watch and the provider's greeting-ended event can each
    // arrive and each decide the message is owed: live, one voicemail was left two. The flag on the activity is
    // only seen once this request commits, so the claim is also held here, where every other path on the node
    // sees it at once.
    private static readonly ConcurrentDictionary<string, byte> _messagesBeingLeft = new(StringComparer.Ordinal);

    // Returns whether the caller's turn was the voicemail's rather than a person's, in which case it has been dealt
    // with here and must not be replied to. Listening has been asked to stop; that is awaited before anything else.
    private async Task<bool> HandleVoicemailTurnAsync(
        VoiceAgentEvent voiceEvent,
        IVoiceAgentMediaProvider media,
        OmnichannelActivity activity,
        AIProfile profile,
        AIChatSession session,
        int turnsBefore,
        bool customerHadSpoken,
        string caller,
        Task stopListening,
        CancellationToken cancellationToken)
    {
        if (!activity.TryGet<VoicemailReached>(out var voicemail))
        {
            // Only a line nobody has yet spoken on can be a greeting. Once the customer has said something, a
            // mention of voicemail is them talking about one.
            if (customerHadSpoken || !VoicemailGreeting.IsRecordedGreeting(caller))
            {
                return false;
            }

            voicemail = new VoicemailReached();
            activity.Put(voicemail);
            await _activityStore.UpdateAsync(activity, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Automated call activity '{ActivityId}' was answered by voicemail, so the assistant will leave a message rather than converse.",
                    activity.ItemId.SanitizeLogValue());
            }
        }

        await stopListening;

        // The message has been left, or is being, and the call is being hung up; whatever the recording hears now
        // is not a turn.
        if (voicemail.MessageLeft || _messagesBeingLeft.ContainsKey(activity.ItemId))
        {
            return true;
        }

        if (!VoicemailGreeting.InvitesTheMessage(caller))
        {
            // The greeting is still going. Listen for the rest of it, and if the line goes quiet instead -- the tone
            // has sounded and it is recording -- the silence watch leaves the message.
            await media.StartTranscriptionAsync(voiceEvent.ProviderCallId, language: "en", commandId: $"ai-tx-vm-{turnsBefore}", cancellationToken);
            await WatchForSilenceAsync(voiceEvent, turnsBefore + 1, VoicemailToneWait);

            return true;
        }

        await LeaveVoicemailAsync(media, voiceEvent.ProviderCallId, activity, voicemail, profile, session, cancellationToken);

        return true;
    }

    // The provider has said who answered. A machine is marked straight away, so the greeting is listened to as a
    // greeting and the call is concluded as unanswered; a person needs nothing, because a conversation is already
    // what the call assumes.
    private async Task OnAnswererDetectedAsync(VoiceAgentEvent voiceEvent, CancellationToken cancellationToken)
    {
        if (voiceEvent.Answerer != VoiceAgentAnswerer.Machine)
        {
            return;
        }

        var activity = await _activityStore.FindByIdAsync(voiceEvent.ActivityId, cancellationToken);

        if (activity is null || activity.Status.IsTerminal() || activity.TryGet<VoicemailReached>(out _))
        {
            return;
        }

        activity.Put(new VoicemailReached { DetectedByProvider = true });
        await _activityStore.UpdateAsync(activity, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The provider detected a machine answering automated call activity '{ActivityId}', so it will be left a message rather than conversed with.",
                activity.ItemId.SanitizeLogValue());
        }
    }

    // The provider heard the greeting end. Recorded rather than acted on here, because the assistant may still be
    // saying its opening line: a message spoken now would queue behind it, and the hangup that follows that line
    // would cut the message off. The speech-ended handler leaves it the moment the line finishes, and a line
    // already listening is on the short voicemail watch, whose silence leaves it.
    private async Task OnMachineGreetingEndedAsync(VoiceAgentEvent voiceEvent, CancellationToken cancellationToken)
    {
        var activity = await _activityStore.FindByIdAsync(voiceEvent.ActivityId, cancellationToken);

        if (activity is null ||
            activity.Status.IsTerminal() ||
            !activity.TryGet<VoicemailReached>(out var voicemail) ||
            voicemail.MessageLeft ||
            voicemail.GreetingEnded)
        {
            return;
        }

        voicemail.GreetingEnded = true;
        activity.Put(voicemail);
        await _activityStore.UpdateAsync(activity, cancellationToken);
    }

    // Leaves the message when the assistant has just finished speaking on a voicemail whose greeting is over.
    private async Task<bool> LeaveVoicemailOnceTheGreetingHasEndedAsync(
        VoiceAgentEvent voiceEvent,
        IVoiceAgentMediaProvider media,
        OmnichannelActivity activity,
        CancellationToken cancellationToken)
    {
        if (!activity.TryGet<VoicemailReached>(out var voicemail) || voicemail.MessageLeft || !voicemail.GreetingEnded)
        {
            return false;
        }

        var (profile, session) = await ResolveConversationAsync(activity, cancellationToken);

        if (profile is null || session is null)
        {
            return false;
        }

        await LeaveVoicemailAsync(media, voiceEvent.ProviderCallId, activity, voicemail, profile, session, cancellationToken);

        return true;
    }

    // Forgets the claim once the call is over, so the set holds only calls that are still up.
    private static void ReleaseVoicemailClaim(string activityId)
    {
        if (!string.IsNullOrEmpty(activityId))
        {
            _messagesBeingLeft.TryRemove(activityId, out _);
        }
    }

    // How long a quiet line is given before the silence is acted on. A voicemail that has not been left its
    // message is listened to for its tone, not given the time a person gets to answer.
    private static TimeSpan? ListeningWait(OmnichannelActivity activity)
        => activity.TryGet<VoicemailReached>(out var voicemail) && !voicemail.MessageLeft
            ? VoicemailToneWait
            : null;

    private async Task LeaveVoicemailAsync(
        IVoiceAgentMediaProvider media,
        string providerCallId,
        OmnichannelActivity activity,
        VoicemailReached voicemail,
        AIProfile profile,
        AIChatSession session,
        CancellationToken cancellationToken)
    {
        // Claimed before the message is composed, not after: whoever claims it second leaves nothing.
        if (voicemail.MessageLeft || !_messagesBeingLeft.TryAdd(activity.ItemId, 0))
        {
            return;
        }

        try
        {
            voicemail.MessageLeft = true;
            activity.Put(voicemail);
            await _activityStore.UpdateAsync(activity, cancellationToken);

            string reply = null;

            try
            {
                (reply, _, _, _) = await CompleteAsync(profile, session, activity, LeavingAVoicemail, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The message is owed whether or not the model can compose it; the plain one is left instead.
                _logger.LogWarning(ex, "Composing the voicemail message for automated call activity '{ActivityId}' failed, so the plain message is left.", activity.ItemId.SanitizeLogValue());
            }

            var message = (reply ?? string.Empty).Replace(HangupMarker, string.Empty, StringComparison.Ordinal).Trim();

            // A question left on a recording is never answered, and is the surest sign the model carried on the
            // conversation instead of leaving a message: live, it asked the voicemail whether the customer wanted a
            // new or used vehicle. The plain message is better than that.
            if (string.IsNullOrWhiteSpace(message) || message.Contains('?', StringComparison.Ordinal))
            {
                message = FallbackVoicemailMessage;
            }

            // Nobody is going to answer it, so the message is the last thing said: the marker hangs up once it has
            // played.
            await StorePromptAsync(session, ChatRole.Assistant, message + " " + HangupMarker, cancellationToken);
            await SpeakAsync(media, providerCallId, activity, message, cancellationToken);
        }
        finally
        {
            // Held until the message is with the provider; the flag this request commits covers every path after.
            ReleaseVoicemailClaim(activity.ItemId);
        }
    }
}
