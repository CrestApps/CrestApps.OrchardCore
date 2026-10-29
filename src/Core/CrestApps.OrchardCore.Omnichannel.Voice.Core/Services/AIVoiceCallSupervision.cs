using CrestApps.Core;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Carries out a supervisor's actions on an AI call: through the live session holding it when it is on this node,
/// and otherwise through the turn-based loop or the provider.
/// </summary>
public sealed class AIVoiceCallSupervision : IAIVoiceCallSupervision
{
    private readonly IOmnichannelActivityStore _activityStore;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAIVoiceCallCommands _commands;
    private readonly VoiceAgentConversationLoop _loop;
    private readonly IVoiceAgentMediaProviderResolver _mediaResolver;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceCallSupervision"/> class.
    /// </summary>
    /// <param name="activityStore">The activity store.</param>
    /// <param name="chatSessionManager">The chat session manager, which records a change of profile.</param>
    /// <param name="commands">Reaches the live session holding a call on this node.</param>
    /// <param name="loop">The conversation loop, which carries out actions on turn-based calls.</param>
    /// <param name="mediaResolver">Resolves the provider carrying a call.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AIVoiceCallSupervision(
        IOmnichannelActivityStore activityStore,
        IAIChatSessionManager chatSessionManager,
        IAIVoiceCallCommands commands,
        VoiceAgentConversationLoop loop,
        IVoiceAgentMediaProviderResolver mediaResolver,
        IClock clock,
        ILogger<AIVoiceCallSupervision> logger)
    {
        _activityStore = activityStore;
        _chatSessionManager = chatSessionManager;
        _commands = commands;
        _loop = loop;
        _mediaResolver = mediaResolver;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> SendNoteAsync(string activityId, string note, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(activityId) || string.IsNullOrWhiteSpace(note))
        {
            return false;
        }

        if (await _commands.SendAsync(activityId, new AIVoiceCallCommand(AIVoiceCallCommandKind.Instruct, note)))
        {
            return true;
        }

        // A live session on another node holds the call: relay the note to it.
        if (await IsHeldByLiveSessionAsync(activityId, cancellationToken))
        {
            return await _commands.RelayAsync(activityId, new AIVoiceCallCommand(AIVoiceCallCommandKind.Instruct, note));
        }

        // No live session holds the call here: a turn-based call reads the note on its next turn.
        return await _loop.LeaveSupervisorNoteAsync(activityId, note, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AIVoiceCallActionResult> TakeOverAsync(string activityId, string targetAgentId, string requestedBy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetAgentId))
        {
            return new(false, "Only a signed-in agent can take a call over.");
        }

        return await HandOverAsync(
            activityId,
            new AITakeOverRequest { TargetAgentId = targetAgentId, RequestedBy = requestedBy, RequestedUtc = _clock.UtcNow },
            "The AI is finishing its sentence; your phone will ring.",
            "Your phone will ring.",
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AIVoiceCallActionResult> TransferToNumberAsync(string activityId, string phoneNumber, string requestedBy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new(false, "A phone number is required.");
        }

        return await HandOverAsync(
            activityId,
            new AITakeOverRequest { TargetPhoneNumber = phoneNumber.Trim(), RequestedBy = requestedBy, RequestedUtc = _clock.UtcNow },
            "The AI is finishing its sentence; the call is being transferred.",
            "The call is being transferred.",
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AIVoiceCallActionResult> SwitchProfileAsync(string activityId, string profileId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return new(false, "An AI profile is required.");
        }

        var activity = await _activityStore.FindByIdAsync(activityId, cancellationToken);

        if (!VoiceAgentConversationLoop.IsLiveAICall(activity))
        {
            return new(false, "The AI is no longer on this call.");
        }

        if (activity.TryGet<AILiveCall>(out var liveCall) && liveCall.IsRealtime)
        {
            return new(false, "A live session keeps the AI profile it was opened with.");
        }

        activity.AIProfileId = profileId;
        await _activityStore.UpdateAsync(activity, cancellationToken);

        if (!string.IsNullOrEmpty(activity.AISessionId))
        {
            var session = await _chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

            if (session is not null)
            {
                session.ProfileId = profileId;
                await _chatSessionManager.SaveAsync(session, cancellationToken);
            }
        }

        return new(true, "The new AI profile answers from the caller's next turn.");
    }

    private async Task<AIVoiceCallActionResult> HandOverAsync(
        string activityId,
        AITakeOverRequest request,
        string liveMessage,
        string turnBasedMessage,
        CancellationToken cancellationToken)
    {
        var activity = await _activityStore.FindByIdAsync(activityId, cancellationToken);

        if (!VoiceAgentConversationLoop.IsLiveAICall(activity) || !activity.TryGet<AILiveCall>(out var liveCall))
        {
            return new(false, "The AI is no longer on this call.");
        }

        // Recorded first: whichever path hands the caller over reads it to ring this person or number instead of a
        // queue.
        activity.Put(request);

        await _activityStore.UpdateAsync(activity, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "'{User}' is handing AI call '{ActivityId}' over.",
                request.RequestedBy.SanitizeLogValue(),
                activityId.SanitizeLogValue());
        }

        // A live session on this node ends the way the transfer tool ends it, and its handoff reads the request.
        if (await _commands.SendAsync(activityId, new AIVoiceCallCommand(AIVoiceCallCommandKind.TakeOver)))
        {
            return new(true, liveMessage);
        }

        if (liveCall.IsRealtime)
        {
            // Held by a live session on another node: relay the request to it.
            return await _commands.RelayAsync(activityId, new AIVoiceCallCommand(AIVoiceCallCommandKind.TakeOver))
                ? new(true, liveMessage)
                : new(false, "The call is held by another server that did not answer; try again in a moment.");
        }

        return await _loop.TakeOverTurnBasedCallAsync(activity, liveCall, cancellationToken)
            ? new(true, turnBasedMessage)
            : new(false, "The call could not be handed over.");
    }

    /// <inheritdoc/>
    public async Task<AIVoiceCallActionResult> EndAsync(string activityId, CancellationToken cancellationToken = default)
    {
        var activity = await _activityStore.FindByIdAsync(activityId, cancellationToken);

        if (!VoiceAgentConversationLoop.IsLiveAICall(activity) || !activity.TryGet<AILiveCall>(out var liveCall))
        {
            return new(false, "The AI is no longer on this call.");
        }

        var media = _mediaResolver.Get(liveCall.ProviderName) ?? _mediaResolver.GetDefault();

        if (media is null)
        {
            return new(false, "The call's provider is not available.");
        }

        // The provider reports the hangup back, and the call is concluded as any other ended call is.
        return await media.HangupAsync(liveCall.ProviderCallId, cancellationToken)
            ? new(true, "The call was ended.")
            : new(false, "The provider did not end the call.");
    }

    private async Task<bool> IsHeldByLiveSessionAsync(string activityId, CancellationToken cancellationToken)
    {
        var activity = await _activityStore.FindByIdAsync(activityId, cancellationToken);

        return VoiceAgentConversationLoop.IsLiveAICall(activity) &&
            activity.TryGet<AILiveCall>(out var liveCall) &&
            liveCall.IsRealtime;
    }
}
