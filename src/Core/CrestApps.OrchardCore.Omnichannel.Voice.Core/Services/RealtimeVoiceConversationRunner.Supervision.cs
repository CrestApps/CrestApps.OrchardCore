using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.AI;
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
        if (command.Kind is not (AIVoiceCallCommandKind.Instruct or AIVoiceCallCommandKind.KeysPressed or AIVoiceCallCommandKind.CallerTurn or AIVoiceCallCommandKind.SwitchProfile) ||
            string.IsNullOrWhiteSpace(command.Text) ||
            callToken.IsCancellationRequested)
        {
            return Task.FromResult(false);
        }

        if (command.Kind == AIVoiceCallCommandKind.SwitchProfile)
        {
            if (_profileManager is null)
            {
                return Task.FromResult(false);
            }

            // The switch reads the new profile and its tools through the call's own document session, which is not
            // safe to share across threads. Live, a switch run beside the tool that asked for it left that session
            // tracking an update it could not flush, and no later turn of the call was stored. A reply in flight
            // (the tool's own, when a tool asked) applies it from the call's flow once it completes; an idle line
            // applies it now, behind the lock the turns are stored under.
            Volatile.Write(ref _pendingProfileSwitch, new PendingProfileSwitch(live, command.Text.Trim()));

            if (Volatile.Read(ref _responseInFlight) == 0)
            {
                _ = Task.Run(() => ApplyPendingProfileSwitchAsync(callToken), CancellationToken.None);
            }

            return Task.FromResult(true);
        }

        if (command.Kind == AIVoiceCallCommandKind.CallerTurn)
        {
            _ = Task.Run(() => DeliverSupervisorNoteAsync(live, ChatRole.User, command.Text.Trim(), callToken), CancellationToken.None);

            return Task.FromResult(true);
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

        _ = Task.Run(() => DeliverSupervisorNoteAsync(live, ChatRole.System, note, callToken), CancellationToken.None);

        return Task.FromResult(true);
    }

    // The call's invocation and conversation, kept so a switch made from a supervisor's request runs in them: the
    // new profile's tools read the call from these scopes, and they are local to the call's own flow.
    private AIInvocationContext _callInvocation;
    private AIConversationContext _callConversation;

    // The switch asked for and not applied yet, and the lock the call's document session is used under.
    private PendingProfileSwitch _pendingProfileSwitch;
    private readonly SemaphoreSlim _callSessionLock = new(1, 1);

    private sealed record PendingProfileSwitch(LiveConversation Live, string ProfileId);

    private void CaptureCallScopes()
    {
        _callInvocation = AIInvocationScope.Current;
        _callConversation = AIConversationScope.Current;
    }

    /// <summary>
    /// Applies the switch asked for, if there is one, and has the new profile greet the caller.
    /// </summary>
    private async Task ApplyPendingProfileSwitchAsync(CancellationToken callToken)
    {
        if (Interlocked.Exchange(ref _pendingProfileSwitch, null) is not { } pending)
        {
            return;
        }

        bool switched;

        try
        {
            await _callSessionLock.WaitAsync(callToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            switched = await SwitchProfileAsync(pending.Live, pending.ProfileId, callToken);
        }
        finally
        {
            _callSessionLock.Release();
        }

        // The new profile speaks first, from a note in the conversation: the session's own instructions already are
        // the new profile's, and a reply asked for with instructions of its own had the model repeat its opening.
        if (switched)
        {
            _ = Task.Run(() => DeliverSupervisorNoteAsync(pending.Live, ChatRole.System, VoiceAgentConversationLoop.ProfileSwitchedTurn, callToken), CancellationToken.None);
        }
    }

    /// <summary>
    /// Switches the live session to another profile: its instructions and tools from the next reply on, in the same
    /// voice. The session's other settings stay as the call opened them.
    /// </summary>
    private async Task<bool> SwitchProfileAsync(LiveConversation live, string profileId, CancellationToken callToken)
    {
        try
        {
            var profile = await _profileManager.FindByIdAsync(profileId, callToken);

            if (profile is null)
            {
                _logger.LogWarning("A supervisor switched a live AI call to profile '{ProfileId}', which does not exist.", profileId);

                return false;
            }

            if (_callInvocation is null || _callConversation is null)
            {
                return false;
            }

            using var invocationScope = AIInvocationScope.Begin(_callInvocation);

            _callConversation.Profile = profile;

            // The call's own session keeps the profile it is on, so the turns it saves next do not put the old one back.
            if (_callConversation.Session is { } chatSession)
            {
                chatSession.ProfileId = profile.ItemId;
            }

            using var conversationScope = AIConversationScope.Begin(_callConversation);

            await _orchestrator.SwitchResourceAsync(live.Current, new RealtimeOrchestrationRequest { Resource = profile }, callToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Switched a live AI call to profile '{ProfileId}'.", profileId);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            // The call ended before the switch.
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A live AI call could not switch to profile '{ProfileId}'.", profileId);

            return false;
        }
    }

    private async Task DeliverCallerKeysAsync(LiveConversation live, CancellationToken callToken)
    {
        try
        {
            var keys = await _callerKeys.TakeWhenSettledAsync(CallerKeysSettle, callToken);

            await DeliverSupervisorNoteAsync(live, ChatRole.User, VoiceAgentConversationLoop.KeysPressedTurn(keys), callToken);
        }
        catch (OperationCanceledException)
        {
            // The call ended while the caller was still pressing keys.
        }
    }

    /// <summary>
    /// Adds a message to the live conversation and asks the model to answer it.
    /// </summary>
    /// <remarks>
    /// The message goes into the conversation rather than into the reply's instructions. Instructions given with
    /// one response replace the session's for it: keys sent that way were answered without the profile's persona
    /// or language (in Spanish, on an English call), and were gone by the next turn, so the AI could not say later
    /// which keys had been pressed.
    /// </remarks>
    private async Task DeliverSupervisorNoteAsync(LiveConversation live, ChatRole role, string message, CancellationToken callToken)
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

            await live.Current.AddMessageAsync(role, message, callToken);
            await live.Current.RequestUnpromptedResponseAsync(cancellationToken: callToken);
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
