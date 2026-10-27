using CrestApps.Core;
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
    private readonly IAIVoiceCallCommands _commands;
    private readonly VoiceAgentConversationLoop _loop;
    private readonly IVoiceAgentMediaProviderResolver _mediaResolver;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceCallSupervision"/> class.
    /// </summary>
    /// <param name="activityStore">The activity store.</param>
    /// <param name="commands">Reaches the live session holding a call on this node.</param>
    /// <param name="loop">The conversation loop, which carries out actions on turn-based calls.</param>
    /// <param name="mediaResolver">Resolves the provider carrying a call.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AIVoiceCallSupervision(
        IOmnichannelActivityStore activityStore,
        IAIVoiceCallCommands commands,
        VoiceAgentConversationLoop loop,
        IVoiceAgentMediaProviderResolver mediaResolver,
        IClock clock,
        ILogger<AIVoiceCallSupervision> logger)
    {
        _activityStore = activityStore;
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

        var activity = await _activityStore.FindByIdAsync(activityId, cancellationToken);

        if (!VoiceAgentConversationLoop.IsLiveAICall(activity) || !activity.TryGet<AILiveCall>(out var liveCall))
        {
            return new(false, "The AI is no longer on this call.");
        }

        // Recorded first: whichever path hands the caller over reads it to ring this person instead of a queue.
        activity.Put(new AITakeOverRequest
        {
            TargetAgentId = targetAgentId,
            RequestedBy = requestedBy,
            RequestedUtc = _clock.UtcNow,
        });

        await _activityStore.UpdateAsync(activity, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "'{User}' is taking AI call '{ActivityId}' over.",
                requestedBy.SanitizeLogValue(),
                activityId.SanitizeLogValue());
        }

        // A live session on this node ends the way the transfer tool ends it, and its handoff rings the person.
        if (await _commands.SendAsync(activityId, new AIVoiceCallCommand(AIVoiceCallCommandKind.TakeOver)))
        {
            return new(true, "The AI is finishing its sentence; your phone will ring.");
        }

        if (liveCall.IsRealtime)
        {
            return new(false, "The call is held by another server; try again in a moment.");
        }

        return await _loop.TakeOverTurnBasedCallAsync(activity, liveCall, cancellationToken)
            ? new(true, "Your phone will ring.")
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
}
