using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// A supervisor's notes to a live session.
/// </summary>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// How long a note waits for the reply in progress to finish before it is given anyway.
    /// </summary>
    internal static readonly TimeSpan SupervisorNoteWait = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Accepts a supervisor's note and acts on it once no reply is in flight, without holding up the request that
    /// sent it.
    /// </summary>
    /// <param name="live">The session holding the call.</param>
    /// <param name="command">The command.</param>
    /// <param name="callToken">The token cancelled when the call ends.</param>
    /// <returns><see langword="true"/> when the command was a note this session will act on.</returns>
    private Task<bool> AcceptSupervisorNoteAsync(LiveConversation live, AIVoiceCallCommand command, CancellationToken callToken)
    {
        if (command.Kind is not (AIVoiceCallCommandKind.Instruct or AIVoiceCallCommandKind.KeysPressed) ||
            string.IsNullOrWhiteSpace(command.Text) ||
            callToken.IsCancellationRequested)
        {
            return Task.FromResult(false);
        }

        var instructions = command.Kind == AIVoiceCallCommandKind.KeysPressed
            ? VoiceAgentConversationLoop.KeysPressedTurn(command.Text.Trim()) + " Answer it as the caller's reply."
            : VoiceAgentConversationLoop.SupervisorNoteGuidance(command.Text.Trim());

        _ = Task.Run(() => DeliverSupervisorNoteAsync(live, instructions, callToken), CancellationToken.None);

        return Task.FromResult(true);
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

            await live.Current.RequestUnpromptedResponseAsync(note, callToken);
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
