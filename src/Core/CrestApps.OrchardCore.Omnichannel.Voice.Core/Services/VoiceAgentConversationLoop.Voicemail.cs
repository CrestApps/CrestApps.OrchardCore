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

        // The message has been left and the call is being hung up; whatever the recording hears now is not a turn.
        if (voicemail.MessageLeft)
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

    private async Task LeaveVoicemailAsync(
        IVoiceAgentMediaProvider media,
        string providerCallId,
        OmnichannelActivity activity,
        VoicemailReached voicemail,
        AIProfile profile,
        AIChatSession session,
        CancellationToken cancellationToken)
    {
        var (reply, _, _, _) = await CompleteAsync(profile, session, activity, LeavingAVoicemail, cancellationToken);

        var message = (reply ?? string.Empty).Replace(HangupMarker, string.Empty, StringComparison.Ordinal).Trim();

        // A question left on a recording is never answered, and is the surest sign the model carried on the
        // conversation instead of leaving a message: live, it asked the voicemail whether the customer wanted a new
        // or used vehicle. The plain message is better than that.
        if (string.IsNullOrWhiteSpace(message) || message.Contains('?', StringComparison.Ordinal))
        {
            message = FallbackVoicemailMessage;
        }

        voicemail.MessageLeft = true;
        activity.Put(voicemail);
        await _activityStore.UpdateAsync(activity, cancellationToken);

        // Nobody is going to answer it, so the message is the last thing said: the marker hangs up once it has played.
        await StorePromptAsync(session, ChatRole.Assistant, message + " " + HangupMarker, cancellationToken);
        await SpeakAsync(media, providerCallId, activity, message, cancellationToken);
    }
}
