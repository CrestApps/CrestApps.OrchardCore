using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Warm and screened transfers of an AI call to a phone number: the number is rung on a side call while the caller
/// holds, whoever answers is briefed (warm) or asked (screened), and then the two are connected or the caller goes
/// back to the AI with the reason.
/// </summary>
/// <remarks>
/// Provider-neutral: the side call, the bridge and the hold audio are the media provider's. A provider that cannot
/// place a side call is given a cold transfer instead. The side call's events reach the loop marked as the side call,
/// and are handled here rather than as the caller's.
/// </remarks>
public sealed partial class VoiceAgentConversationLoop
{
    /// <summary>
    /// What whoever answers a screened transfer is asked after the briefing.
    /// </summary>
    internal const string ScreeningQuestion = "Press 1 to take the call now, or 2 if you cannot.";

    /// <summary>
    /// Starts a warm or screened transfer, or makes a cold one when the provider cannot place a side call.
    /// </summary>
    /// <param name="voiceEvent">The caller's call.</param>
    /// <param name="media">The media provider carrying it.</param>
    /// <param name="activity">The call's activity.</param>
    /// <param name="request">The transfer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when a side call is ringing; <see langword="false"/> when the caller should be
    /// transferred cold instead.</returns>
    private async Task<bool> StartAttendedTransferAsync(
        VoiceAgentEvent voiceEvent,
        IVoiceAgentMediaProvider media,
        OmnichannelActivity activity,
        AITakeOverRequest request,
        CancellationToken cancellationToken)
    {
        // The AI listens to nothing while the caller holds.
        await media.StopTranscriptionAsync(voiceEvent.ProviderCallId, cancellationToken);

        var sideCallId = await media.DialSideCallAsync(new VoiceAgentSideCallRequest
        {
            ProviderCallId = voiceEvent.ProviderCallId,
            ActivityId = activity.ItemId,
            PhoneNumber = request.TargetPhoneNumber,
            CallerId = request.ShowCallerNumber ? activity.PreferredDestination : null,
            RingTimeoutSeconds = request.RingTimeoutSeconds > 0 ? request.RingTimeoutSeconds : 30,
        }, cancellationToken);

        if (string.IsNullOrEmpty(sideCallId))
        {
            _logger.LogWarning(
                "A {Mode} transfer of AI voice activity '{ActivityId}' could not ring the number; transferring the caller directly.",
                request.TransferMode,
                activity.ItemId.SanitizeLogValue());

            return false;
        }

        var holdAudioPlaying = !string.IsNullOrWhiteSpace(request.HoldAudio) &&
            await media.PlayAudioAsync(voiceEvent.ProviderCallId, request.HoldAudio, loop: true, cancellationToken);

        var transfer = new AIAttendedTransfer
        {
            SideCallId = sideCallId,
            PhoneNumber = request.TargetPhoneNumber,
            Mode = request.TransferMode,
            Briefing = string.IsNullOrWhiteSpace(request.Briefing)
                ? await WriteBriefingAsync(activity, cancellationToken)
                : request.Briefing.Trim(),
            Introduction = string.IsNullOrWhiteSpace(request.Introduction) ? null : request.Introduction.Trim(),
            HoldAudioPlaying = holdAudioPlaying,
            StartedUtc = _clock.UtcNow,
        };

        activity.Put(transfer);
        await _activityStore.UpdateAsync(activity, cancellationToken);

        // Without hold audio the caller is at least told why it has gone quiet.
        if (!holdAudioPlaying)
        {
            await SpeakAsync(media, voiceEvent.ProviderCallId, activity, S["Please hold while I connect you."].Value, cancellationToken);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "AI voice activity '{ActivityId}' started a {Mode} transfer; the destination is ringing.",
                activity.ItemId.SanitizeLogValue(),
                transfer.Mode);
        }

        return true;
    }

    /// <summary>
    /// Handles what a warm or screened transfer's side call needs before the caller's event is handled as usual.
    /// </summary>
    /// <param name="voiceEvent">The event.</param>
    /// <param name="media">The media provider carrying the call.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the event was the transfer's and nothing more is to be done with it.</returns>
    private async Task<bool> HandleAttendedTransferEventAsync(VoiceAgentEvent voiceEvent, IVoiceAgentMediaProvider media, CancellationToken cancellationToken)
    {
        if (!voiceEvent.IsSideCall &&
            voiceEvent.Kind is not (VoiceAgentEventKind.SpeechEnded or VoiceAgentEventKind.Hangup or VoiceAgentEventKind.Transcription or VoiceAgentEventKind.DigitsReceived))
        {
            return false;
        }

        var activity = await _activityStore.FindByIdAsync(voiceEvent.ActivityId, cancellationToken);

        if (activity is null || !activity.TryGet<AIAttendedTransfer>(out var transfer))
        {
            // A side call whose transfer is over has nothing left to do; a caller's event is the caller's.
            return voiceEvent.IsSideCall;
        }

        if (!voiceEvent.IsSideCall)
        {
            if (voiceEvent.Kind == VoiceAgentEventKind.Hangup)
            {
                // The caller gave up holding: nobody is left to connect the destination to.
                await media.HangupAsync(transfer.SideCallId, cancellationToken);
                activity.Remove<AIAttendedTransfer>();
                await _activityStore.UpdateAsync(activity, cancellationToken);

                return false;
            }

            // The caller is holding: the end of "please hold" must not start listening, and nothing they say is a turn.
            return true;
        }

        if (!string.Equals(voiceEvent.ProviderCallId, transfer.SideCallId, StringComparison.Ordinal))
        {
            return true;
        }

        var callerCallId = voiceEvent.PeerProviderCallId;

        switch (voiceEvent.Kind)
        {
            case VoiceAgentEventKind.Answered:
                transfer.Answered = true;
                activity.Put(transfer);
                await _activityStore.UpdateAsync(activity, cancellationToken);

                await SpeakToSideCallAsync(media, voiceEvent.ProviderCallId, activity, transfer, cancellationToken);
                break;

            case VoiceAgentEventKind.AnswererDetected when voiceEvent.Answerer == VoiceAgentAnswerer.Machine:
                // A voicemail cannot take the caller.
                await media.HangupAsync(voiceEvent.ProviderCallId, cancellationToken);
                await ReturnToAIAsync(media, callerCallId, activity, transfer, "their voicemail answered", cancellationToken);
                break;

            case VoiceAgentEventKind.SpeechEnded when transfer.Mode == AICallTransferMode.Warm:
                // The briefing has been heard: connect them.
                await ConnectAsync(media, callerCallId, activity, transfer, cancellationToken);
                break;

            case VoiceAgentEventKind.DigitsReceived when transfer.Mode == AICallTransferMode.Screened:
                if (string.Equals(voiceEvent.Digits?.Trim(), "1", StringComparison.Ordinal))
                {
                    await ConnectAsync(media, callerCallId, activity, transfer, cancellationToken);
                }
                else
                {
                    await media.HangupAsync(voiceEvent.ProviderCallId, cancellationToken);
                    await ReturnToAIAsync(media, callerCallId, activity, transfer, "they could not take the call", cancellationToken);
                }

                break;

            case VoiceAgentEventKind.Hangup:
                var reason = !transfer.Answered
                    ? voiceEvent.HangupCause == HangupCause.Busy ? "the line was busy" : "nobody answered"
                    : transfer.Mode == AICallTransferMode.Screened ? "they hung up without accepting" : "they hung up before being connected";

                await ReturnToAIAsync(media, callerCallId, activity, transfer, reason, cancellationToken);
                break;
        }

        return true;
    }

    private async Task SpeakToSideCallAsync(IVoiceAgentMediaProvider media, string sideCallId, OmnichannelActivity activity, AIAttendedTransfer transfer, CancellationToken cancellationToken)
    {
        if (transfer.Mode == AICallTransferMode.Screened)
        {
            // Gathered rather than spoken: the answer comes back as the key they press.
            await media.GatherAsync(sideCallId, $"{transfer.Briefing} {ScreeningQuestion}", validDigits: "12", cancellationToken);

            return;
        }

        await SpeakAsync(media, sideCallId, activity, transfer.Briefing, cancellationToken);
    }

    private async Task ConnectAsync(IVoiceAgentMediaProvider media, string callerCallId, OmnichannelActivity activity, AIAttendedTransfer transfer, CancellationToken cancellationToken)
    {
        if (transfer.HoldAudioPlaying)
        {
            await media.StopAudioAsync(callerCallId, cancellationToken);
        }

        if (!await media.BridgeAsync(callerCallId, transfer.SideCallId, cancellationToken))
        {
            await media.HangupAsync(transfer.SideCallId, cancellationToken);
            await ReturnToAIAsync(media, callerCallId, activity, transfer, "the call could not be connected", cancellationToken);

            return;
        }

        await RecordHandoffAsync(activity, cancellationToken, current =>
        {
            current.Remove<AIAttendedTransfer>();
            current.Put(new AICallTransfer
            {
                PhoneNumber = transfer.PhoneNumber,
                Mode = transfer.Mode,
                TransferredUtc = _clock.UtcNow,
            });
        });

        if (!string.IsNullOrEmpty(transfer.Introduction))
        {
            await SpeakAsync(media, callerCallId, activity, transfer.Introduction, cancellationToken);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "AI voice activity '{ActivityId}' was connected to the destination of its {Mode} transfer.",
                activity.ItemId.SanitizeLogValue(),
                transfer.Mode);
        }
    }

    /// <summary>
    /// Gives the caller back to the AI after a warm or screened transfer did not connect, with the reason, so it can
    /// tell them and offer another way.
    /// </summary>
    private async Task ReturnToAIAsync(
        IVoiceAgentMediaProvider media,
        string callerCallId,
        OmnichannelActivity activity,
        AIAttendedTransfer transfer,
        string reason,
        CancellationToken cancellationToken)
    {
        if (transfer.HoldAudioPlaying)
        {
            await media.StopAudioAsync(callerCallId, cancellationToken);
        }

        activity.Remove<AIAttendedTransfer>();
        activity.Remove<AITakeOverRequest>();

        // From here the turn-based loop holds the call, whichever engine started it: a live session ended when it
        // asked for the transfer.
        if (activity.TryGet<AILiveCall>(out var liveCall) && liveCall.IsRealtime)
        {
            liveCall.IsRealtime = false;
            activity.Put(liveCall);
        }

        await _activityStore.UpdateAsync(activity, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The {Mode} transfer of AI voice activity '{ActivityId}' did not connect ({Reason}); the caller is back with the AI.",
                transfer.Mode,
                activity.ItemId.SanitizeLogValue(),
                reason);
        }

        var (profile, session) = await ResolveConversationAsync(activity, cancellationToken);
        string reply = null;

        if (profile is not null && session is not null)
        {
            (reply, _, _, _) = await CompleteAsync(profile, session, activity, TransferFailedGuidance(reason), cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(reply))
        {
            reply = S["I'm sorry, I couldn't reach them just now. Is there anything else I can help you with, or would you like someone to call you back?"].Value;
        }

        if (session is not null)
        {
            await StorePromptAsync(session, ChatRole.Assistant, reply, cancellationToken);
        }

        // The end of this line starts listening to the caller again, as any reply's does.
        if (!await SpeakAsync(media, callerCallId, activity, reply.Replace(HangupMarker, string.Empty, StringComparison.Ordinal), cancellationToken))
        {
            await media.HangupAsync(callerCallId, cancellationToken);
        }
    }

    /// <summary>
    /// The turn the AI answers when a transfer did not connect.
    /// </summary>
    /// <param name="reason">Why it did not connect.</param>
    /// <returns>The turn.</returns>
    internal static string TransferFailedGuidance(string reason)
        => $"[The transfer did not go through: {reason}. You are back on the line with the caller. Tell them briefly, then offer to take a message, arrange a callback, or help another way.]";

    private async Task<string> WriteBriefingAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        var fallback = S["Hello, this is an automated assistant with a caller who asked to speak with you."].Value;
        var (profile, session) = await ResolveConversationAsync(activity, cancellationToken);

        if (profile is null || session is null)
        {
            return fallback;
        }

        try
        {
            var (briefing, _, _, _) = await CompleteAsync(profile, session, activity, BriefingGuidance, cancellationToken);

            return string.IsNullOrWhiteSpace(briefing)
                ? fallback
                : briefing.Replace(HangupMarker, string.Empty, StringComparison.Ordinal).Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The briefing for the transfer of AI voice activity '{ActivityId}' could not be written.", activity.ItemId.SanitizeLogValue());

            return fallback;
        }
    }

    /// <summary>
    /// The turn the AI answers to write a warm or screened transfer's briefing.
    /// </summary>
    internal const string BriefingGuidance = "[You are about to hand this caller to a colleague on another line, who cannot hear the conversation so far. Write what you will say to that colleague privately, in at most three short spoken sentences: say you are the automated assistant, who is calling if you know, and what they need. Do not address the caller and call no tools.]";
}
