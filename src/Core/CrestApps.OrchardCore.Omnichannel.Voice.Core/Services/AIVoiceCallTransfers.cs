using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Transfers a live AI call to a phone number: through the live session holding it, on this node or another, and
/// otherwise through the turn-based loop.
/// </summary>
public sealed class AIVoiceCallTransfers : IAIVoiceCallTransfers
{
    private readonly IOmnichannelActivityStore _activityStore;
    private readonly IAIVoiceCallCommands _commands;
    private readonly VoiceAgentConversationLoop _loop;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceCallTransfers"/> class.
    /// </summary>
    /// <param name="activityStore">The activity store.</param>
    /// <param name="commands">Reaches the live session holding a call.</param>
    /// <param name="loop">The conversation loop, which transfers turn-based calls.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AIVoiceCallTransfers(
        IOmnichannelActivityStore activityStore,
        IAIVoiceCallCommands commands,
        VoiceAgentConversationLoop loop,
        IClock clock,
        ILogger<AIVoiceCallTransfers> logger)
    {
        _activityStore = activityStore;
        _commands = commands;
        _loop = loop;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AIVoiceCallActionResult> TransferAsync(string activityId, AITakeOverRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TargetPhoneNumber))
        {
            return new(false, "A phone number is required.");
        }

        var activity = await _activityStore.FindByIdAsync(activityId, cancellationToken);

        if (!VoiceAgentConversationLoop.IsLiveAICall(activity) || !activity.TryGet<AILiveCall>(out var liveCall))
        {
            return new(false, "The AI is no longer on this call.");
        }

        if (activity.Has<AIAttendedTransfer>())
        {
            return new(false, "A transfer is already under way on this call.");
        }

        request.TargetAgentId = null;
        request.TargetPhoneNumber = request.TargetPhoneNumber.Trim();

        if (request.RequestedUtc == default)
        {
            request.RequestedUtc = _clock.UtcNow;
        }

        // Recorded first: whichever path hands the caller over reads it to ring this number, and how.
        activity.Put(request);
        await _activityStore.UpdateAsync(activity, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "'{User}' is transferring AI call '{ActivityId}' to a phone number ({Mode}).",
                request.RequestedBy.SanitizeLogValue(),
                activityId.SanitizeLogValue(),
                request.TransferMode);
        }

        var takeOver = new AIVoiceCallCommand(AIVoiceCallCommandKind.TakeOver);

        // A live session ends the way the transfer tool ends it, and the handoff that follows reads the request.
        if (liveCall.IsRealtime)
        {
            return await _commands.RelayAsync(activityId, takeOver)
                ? new(true, "The AI is finishing its sentence; the call is being transferred.")
                : new(false, "The server holding the call did not answer; try again in a moment.");
        }

        return await _loop.TakeOverTurnBasedCallAsync(activity, liveCall, cancellationToken)
            ? new(true, request.TransferMode == AICallTransferMode.Cold ? "The call is being transferred." : "The number is ringing; the caller is holding.")
            : new(false, "The call could not be transferred.");
    }
}
