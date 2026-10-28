using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// A supervisor's notes to a live session, and the keys the caller presses.
/// </summary>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// How long a note waits for the reply in progress to finish before it is given anyway.
    /// </summary>
    internal static readonly TimeSpan SupervisorNoteWait = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How long the caller must stop pressing keys before the keys they pressed are answered.
    /// </summary>
    internal static readonly TimeSpan CallerKeysSettle = TimeSpan.FromMilliseconds(1500);

    private readonly CallerKeyBuffer _callerKeys = new();

    /// <summary>
    /// Accepts a supervisor's note or the caller's keys and acts on them once no reply is in flight, without holding
    /// up the request that sent them.
    /// </summary>
    /// <param name="live">The session holding the call.</param>
    /// <param name="command">The command.</param>
    /// <param name="callToken">The token cancelled when the call ends.</param>
    /// <returns><see langword="true"/> when the command was one this session will act on.</returns>
    private Task<bool> AcceptSupervisorNoteAsync(LiveConversation live, AIVoiceCallCommand command, CancellationToken callToken)
    {
        if (command.Kind is not (AIVoiceCallCommandKind.Instruct or AIVoiceCallCommandKind.KeysPressed) ||
            string.IsNullOrWhiteSpace(command.Text) ||
            callToken.IsCancellationRequested)
        {
            return Task.FromResult(false);
        }

        if (command.Kind == AIVoiceCallCommandKind.KeysPressed)
        {
            // Each key arrives as its own command: the first one of a burst answers for all of them.
            if (_callerKeys.Add(command.Text.Trim()))
            {
                _ = Task.Run(() => DeliverCallerKeysAsync(live, callToken), CancellationToken.None);
            }

            return Task.FromResult(true);
        }

        var note = VoiceAgentConversationLoop.SupervisorNoteGuidance(command.Text.Trim());

        _ = Task.Run(() => DeliverSupervisorNoteAsync(live, note, callToken), CancellationToken.None);

        return Task.FromResult(true);
    }

    private async Task DeliverCallerKeysAsync(LiveConversation live, CancellationToken callToken)
    {
        try
        {
            var keys = await _callerKeys.TakeWhenSettledAsync(CallerKeysSettle, callToken);

            await DeliverSupervisorNoteAsync(live, VoiceAgentConversationLoop.KeysPressedTurn(keys) + " Answer it as the caller's reply.", callToken);
        }
        catch (OperationCanceledException)
        {
            // The call ended while the caller was still pressing keys.
        }
    }

    private async Task DeliverSupervisorNoteAsync(LiveConversation live, string note, CancellationToken callToken)
    {
        try
        {
            // Asking for a reply while one is being spoken produces two replies over each other, so the note waits
            // for the current one, and goes anyway once it has waited long enough.
            var waitedUntil = DateTime.UtcNow + SupervisorNoteWait;

            while (Volatile.Read(ref _responseInFlight) == 1 && DateTime.UtcNow < waitedUntil)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), callToken);
            }

            // Instructions given with one response replace the session's for it: a note given on its own was
            // answered by a model that had lost the profile's persona and language (a caller's keys were answered
            // in Spanish on an English call).
            await live.Current.RequestUnpromptedResponseAsync(WithSessionInstructions(note), callToken);
        }
        catch (OperationCanceledException)
        {
            // The call ended before the note could be given.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A supervisor's note could not be given to the live AI session.");
        }
    }
}
