using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Sending a caller to voicemail: guaranteeing a live leg, greeting them, and recording the message.
/// </summary>
public sealed partial class TelnyxTelephonyProvider
{
    /// <inheritdoc/>
    public async Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        var callId = call?.CallId;
        var invalid = RequireCallId(callId, S["A call id is required to send the call to voicemail."].Value);

        if (invalid is not null)
        {
            return invalid;
        }

        if (!_options.IsConfigured)
        {
            return NotConfigured();
        }

        // A ringing inbound call has to be answered before a greeting can be played or a message recorded. A leg
        // that is already up does not, and Telnyx refuses the attempt outright: "Can not issue an answer command
        // on an outbound call" (90102).
        //
        // That refusal used to abort the whole thing, so a caller who reached voicemail from a queue — already
        // connected, by definition — was never greeted and never recorded. They heard the hold music stop and
        // then nothing. The answer exists to guarantee a live leg, so a refusal that means the leg is already
        // live is the goal, not a failure.
        var answered = await ExecuteActionAsync(callId, "answer", body: null, () => null, cancellationToken, succeedWhenMissing: true);

        if (!answered.Succeeded && !await IsAlreadyLiveAsync(callId, cancellationToken))
        {
            return answered;
        }

        // Correlate the voicemail to its interaction. The routing engine carries the interaction id under the
        // provider-command metadata key; an agent-initiated "send to voicemail" from the soft phone carries it
        // under the incoming-call metadata key, so accept either.
        var voicemailInteractionId =
            TryGetMetadataString(call.Metadata, ContactCenterConstants.CommandMetadata.InteractionId)
            ?? TryGetMetadataString(call.Metadata, "interactionId");
        var recipientUserId = TryGetMetadataString(call.Metadata, "voicemailRecipientUserId");

        // Play the recipient agent's greeting BEFORE recording, and only start recording once the greeting has
        // finished. The greeting carries a voicemail-greeting client_state; Telnyx echoes it on the greeting's
        // call.speak.ended / call.playback.ended webhook, which the webhook pipeline turns into a record_start with
        // a leading beep (TelnyxVoicemailRecordingStarter). This keeps the spoken greeting out of the caller's
        // recorded message and gives the caller the "after the beep" tone the greeting promises. A recorded/uploaded
        // audio greeting (a publicly reachable URL) is played with playback_start; otherwise the per-agent (or
        // default) text greeting is spoken with text-to-speech.
        //
        // Best-effort: a greeting hiccup must not fail the overall action, which has already answered the call.
        var greetingClientState = string.IsNullOrWhiteSpace(voicemailInteractionId)
            ? null
            : TelnyxRecordingClientState.ForVoicemailGreeting(voicemailInteractionId, recipientUserId).ToClientState();

        // Without an interaction id there is nothing to correlate the eventual recording to and no client_state to
        // ride the greeting-ended webhook, so fall back to recording immediately (uncorrelated, best-effort) rather
        // than losing the message entirely.
        if (greetingClientState is null)
        {
            await ExecuteActionAsync(
                callId,
                "record_start",
                new Dictionary<string, object>
                {
                    ["format"] = TelnyxConstants.Recording.Format,
                    ["channels"] = "single",
                    ["play_beep"] = true,
                },
                () => null,
                cancellationToken);
        }

        // Prefer a Telnyx-hosted greeting (media_name in Telnyx Media Storage) the agent recorded or uploaded, then a
        // hosted audio URL, then spoken text. The media_name path needs no publicly reachable URL of ours.
        var greetingMediaName = TryGetMetadataString(call.Metadata, ContactCenterConstants.Voicemail.GreetingMediaNameMetadataKey);
        var greetingMediaUrl = TryGetMetadataString(call.Metadata, ContactCenterConstants.Voicemail.GreetingMediaUrlMetadataKey);

        if (!string.IsNullOrWhiteSpace(greetingMediaName) || !string.IsNullOrWhiteSpace(greetingMediaUrl))
        {
            var playbackBody = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(greetingMediaName))
            {
                playbackBody["media_name"] = greetingMediaName;
            }
            else
            {
                playbackBody["audio_url"] = greetingMediaUrl;
            }

            if (greetingClientState is not null)
            {
                playbackBody["client_state"] = greetingClientState;
            }

            await ExecuteActionAsync(callId, "playback_start", playbackBody, () => null, cancellationToken);
        }
        else
        {
            var greetingText = TryGetMetadataString(call.Metadata, ContactCenterConstants.Voicemail.GreetingTextMetadataKey);
            var greeting = !string.IsNullOrWhiteSpace(greetingText)
                ? greetingText
                : S["Please leave your message after the tone, then hang up."].Value;

            var speakBody = new Dictionary<string, object>
            {
                ["payload"] = greeting,
                ["payload_type"] = "text",
                ["voice"] = "female",
                ["language"] = "en-US",
            };

            if (greetingClientState is not null)
            {
                speakBody["client_state"] = greetingClientState;
            }

            await ExecuteActionAsync(callId, "speak", speakBody, () => null, cancellationToken);
        }

        return TelephonyResult.Success(BuildCall(callId, CallState.Connected, call.Metadata, CallDirection.Inbound));
    }

    /// <summary>
    /// Whether the leg is already up, and so did not need the answer that was just refused.
    /// </summary>
    /// <remarks>
    /// Asked of the provider rather than inferred from the error text, because the reasons an answer is refused
    /// ("already answered", "outbound call", a leg that has since hung up) are not distinguishable from a
    /// message, and treating a genuinely dead leg as live would leave voicemail talking to nobody.
    /// </remarks>
    /// <param name="callId">The leg to check.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private async Task<bool> IsAlreadyLiveAsync(string callId, CancellationToken cancellationToken)
    {
        try
        {
            var (response, data) = await _apiClient.GetCallAsync(callId, cancellationToken);

            if (!response.Succeeded)
            {
                return false;
            }

            return data is not null &&
                data.Value.TryGetProperty("data", out var callData) &&
                callData.ValueKind == JsonValueKind.Object &&
                callData.TryGetProperty("is_alive", out var aliveElement) &&
                aliveElement.ValueKind == JsonValueKind.True;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
