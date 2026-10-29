using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What a supervisor can do to a live AI call: send the AI a note, or take the call over.
/// </summary>
public sealed partial class VoiceAgentConversationLoop
{
    /// <summary>
    /// The reason recorded when a supervisor takes an AI call over.
    /// </summary>
    internal const string SupervisorTookOverReason = "A supervisor took the call over from the AI assistant.";

    /// <summary>
    /// Wraps a supervisor's note as guidance the model acts on without repeating it.
    /// </summary>
    /// <param name="note">The supervisor's note.</param>
    /// <returns>The guidance.</returns>
    internal static string SupervisorNoteGuidance(string note)
        => "A supervisor who is following this call sent you this note. Act on it in your next reply, in your own words, " +
            "and never mention the supervisor or the note: " + note;

    /// <summary>
    /// Leaves a note for the AI on a turn-based call, read on the AI's next turn.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="note">The note.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the note was kept for a call that is still the AI's.</returns>
    internal async Task<bool> LeaveSupervisorNoteAsync(string activityId, string note, CancellationToken cancellationToken)
    {
        var activity = await _activityStore.FindByIdAsync(activityId, cancellationToken);

        if (!IsLiveAICall(activity))
        {
            return false;
        }

        activity.Alter<PendingSupervisorNotes>(notes => notes.Notes.Add(note.Trim()));
        await _activityStore.UpdateAsync(activity, cancellationToken);

        return true;
    }

    /// <summary>
    /// Hands a turn-based AI call to the person taking it over: the AI stops listening and the caller is rung
    /// through to them directly.
    /// </summary>
    /// <param name="activity">The call's activity, carrying the <see cref="AITakeOverRequest"/>.</param>
    /// <param name="liveCall">The provider call.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the caller was handed over.</returns>
    internal async Task<bool> TakeOverTurnBasedCallAsync(OmnichannelActivity activity, AILiveCall liveCall, CancellationToken cancellationToken)
    {
        var media = _mediaResolver.Get(liveCall.ProviderName) ?? _mediaResolver.GetDefault();

        if (media is null)
        {
            return false;
        }

        // Whatever the AI was listening to or about to say stops; the next voice the caller hears is the person's.
        await media.StopTranscriptionAsync(liveCall.ProviderCallId, cancellationToken);

        await PerformVoiceHandoffAsync(
            new VoiceAgentEvent
            {
                ActivityId = activity.ItemId,
                ProviderName = liveCall.ProviderName,
                ProviderCallId = liveCall.ProviderCallId,
            },
            media,
            activity,
            _clock.UtcNow,
            cancellationToken);

        var current = await _activityStore.FindByIdAsync(activity.ItemId, cancellationToken);

        return current?.AiEscalated == true || current?.Status.IsTerminal() == true;
    }

    /// <summary>
    /// What the AI hears when the caller presses keys: a line it answers like anything the caller said.
    /// </summary>
    /// <param name="digits">The keys.</param>
    /// <returns>The caller's turn.</returns>
    internal static string KeysPressedTurn(string digits)
        => $"[The caller pressed these keys on their phone: {digits}]";

    /// <summary>
    /// The turn a profile answers when the call has just been handed to it from another one.
    /// </summary>
    internal const string ProfileSwitchedTurn =
        "[This call has just been handed to you from another assistant, and the customer is waiting for you. Greet them " +
        "in one short sentence, say who you are, and carry on with what they need from the conversation so far. Do not " +
        "repeat what was already said or ask them to start again.]";

    /// <summary>
    /// The caller pressed keys: a live session hears about it through the session holding the call, and a turn-based
    /// call collects them until the caller stops, then takes them as one turn.
    /// </summary>
    /// <param name="voiceEvent">The provider event.</param>
    private async Task OnDigitsReceivedAsync(VoiceAgentEvent voiceEvent)
    {
        var digits = voiceEvent.Digits?.Trim();

        if (string.IsNullOrEmpty(digits))
        {
            return;
        }

        if (await _voiceCallCommands.SendAsync(voiceEvent.ActivityId, new AIVoiceCallCommand(AIVoiceCallCommandKind.KeysPressed, digits)))
        {
            return;
        }

        // Pressing keys is the caller taking their turn: the line is not silent, and a reply still being written to
        // what they said before is overtaken.
        _silenceWatchdog.CallerHeard(voiceEvent.ActivityId);
        _callerKeys.Collect(voiceEvent);
    }

    /// <summary>
    /// Answers the keys the caller pressed on a turn-based call, once they have stopped pressing, as one turn.
    /// </summary>
    /// <param name="firstKey">The event of the first key, which names the call.</param>
    /// <param name="digits">Every key pressed, in order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    internal Task OnCallerKeysAsync(VoiceAgentEvent firstKey, string digits, CancellationToken cancellationToken)
    {
        var media = _mediaResolver.Get(firstKey.ProviderName) ?? _mediaResolver.GetDefault();

        if (media is null || string.IsNullOrEmpty(digits))
        {
            return Task.CompletedTask;
        }

        return OnTranscriptionAsync(
            new VoiceAgentEvent
            {
                Kind = VoiceAgentEventKind.Transcription,
                ActivityId = firstKey.ActivityId,
                ProviderName = firstKey.ProviderName,
                ProviderCallId = firstKey.ProviderCallId,
                TranscriptionText = KeysPressedTurn(digits),
                TranscriptionIsFinal = true,
                OccurredUtc = firstKey.OccurredUtc,
            },
            media,
            cancellationToken);
    }

    /// <summary>
    /// Transfers the caller to a phone number through the provider, and records that the AI is no longer on the call.
    /// </summary>
    /// <param name="voiceEvent">The call.</param>
    /// <param name="media">The media provider carrying it.</param>
    /// <param name="activity">The call's activity.</param>
    /// <param name="phoneNumber">The number to ring.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task TransferToNumberAsync(
        VoiceAgentEvent voiceEvent,
        IVoiceAgentMediaProvider media,
        OmnichannelActivity activity,
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        if (!await media.TransferAsync(voiceEvent.ProviderCallId, phoneNumber, cancellationToken))
        {
            _logger.LogWarning(
                "AI voice activity '{ActivityId}' could not be transferred to a phone number; the call is ended.",
                activity.ItemId.SanitizeLogValue());

            await media.HangupAsync(voiceEvent.ProviderCallId, cancellationToken);

            return;
        }

        // From here the caller talks to whoever answers: the AI listens to nothing more on this call.
        await RecordHandoffAsync(activity, cancellationToken, current => current.Put(new AICallTransfer
        {
            PhoneNumber = phoneNumber,
            TransferredUtc = _clock.UtcNow,
        }));

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "AI voice activity '{ActivityId}' was transferred to a phone number.",
                activity.ItemId.SanitizeLogValue());
        }
    }

    /// <summary>
    /// Whether an activity is an AI call still being held by the AI.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <returns><see langword="true"/> when the AI is on the call.</returns>
    public static bool IsLiveAICall(OmnichannelActivity activity)
        => activity is not null &&
            activity.InteractionType == ActivityInteractionType.Automated &&
            activity.Status == ActivityStatus.InProgress &&
            !activity.AiEscalated &&
            string.Equals(activity.Channel, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads and clears the notes a supervisor left for the AI's next turn.
    /// </summary>
    /// <param name="activity">The call's activity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The notes, oldest first.</returns>
    private async Task<IReadOnlyList<string>> TakeSupervisorNotesAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        // Re-read: the note was written by the supervisor's request, after this turn loaded the activity.
        var current = activity is null ? null : await _activityStore.FindByIdAsync(activity.ItemId, cancellationToken);

        if (current is null || !current.TryGet<PendingSupervisorNotes>(out var pending) || pending.Notes is not { Count: > 0 })
        {
            return [];
        }

        current.Remove<PendingSupervisorNotes>();
        await _activityStore.UpdateAsync(current, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Giving the AI on activity '{ActivityId}' {Count} note(s) from a supervisor.",
                activity.ItemId.SanitizeLogValue(),
                pending.Notes.Count);
        }

        return pending.Notes;
    }
}
