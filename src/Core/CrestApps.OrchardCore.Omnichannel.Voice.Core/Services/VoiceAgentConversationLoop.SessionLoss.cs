using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What happens to a caller whose assistant's live session was lost, and could not be brought back, while they
/// were still on the line.
/// </summary>
/// <remarks>
/// Live, a provider error ended the session partway through the opening. Nothing had been decided — no transfer,
/// no goodbye — so nothing was done: the media stream stopped, the call stayed up, and the caller listened to
/// fifty seconds of silence before hanging up. The call was then concluded as though it had run its course.
/// </remarks>
public sealed partial class VoiceAgentConversationLoop
{
    /// <summary>
    /// Takes the call over from a lost session: hands the caller to a person when this call can reach one, and
    /// otherwise apologizes and ends the call, so the line is never left silent.
    /// </summary>
    /// <param name="voiceEvent">The call.</param>
    /// <param name="media">The media provider carrying it.</param>
    /// <param name="activity">The activity behind the call.</param>
    /// <param name="lostUtc">When the session was lost.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private async Task TakeOverAfterALostSessionAsync(
        VoiceAgentEvent voiceEvent,
        IVoiceAgentMediaProvider media,
        OmnichannelActivity activity,
        DateTime lostUtc,
        CancellationToken cancellationToken)
    {
        // Recorded first, whatever happens next. The call is concluded later from the hangup by a review that reads
        // only the transcript, and a transcript that stops mid-sentence reads to it like a customer who lost
        // interest -- which is how the call this was written for came to be concluded "Done" and never tried again.
        activity.Put(new AIVoiceSessionLost { LostUtc = lostUtc });
        await _activityStore.UpdateAsync(activity, cancellationToken);

        var (handoffService, _) = await ResolveVoiceHandoffAsync(activity, cancellationToken);

        if (handoffService is not null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "The assistant on AI voice activity '{ActivityId}' lost its session; handing the caller to a live agent.",
                    activity.ItemId.SanitizeLogValue());
            }

            // The same path a transfer the model asked for takes, and it tells the caller what is happening.
            await PerformVoiceHandoffAsync(voiceEvent, media, activity, lostUtc, cancellationToken);

            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The assistant on AI voice activity '{ActivityId}' lost its session and this call has no agent to hand to; apologizing and ending the call.",
                activity.ItemId.SanitizeLogValue());
        }

        var apology = S["I'm sorry, I'm having some trouble on my end, so I'll need to let you go. We'll call you back soon. Goodbye."].Value;

        // Stored with the hangup marker, as the turn-based goodbye is, so the end of this line hangs up the call
        // rather than leaving it open once the apology has been heard.
        if (!string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            var session = await _chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

            if (session is not null)
            {
                await StorePromptAsync(session, ChatRole.Assistant, apology + " " + HangupMarker, cancellationToken);
            }
        }

        // A line that cannot be spoken leaves nothing to wait for, so the call is ended straight away instead.
        if (!await SpeakAsync(media, voiceEvent.ProviderCallId, activity, apology, cancellationToken))
        {
            await media.HangupAsync(voiceEvent.ProviderCallId, cancellationToken);
        }
    }
}
