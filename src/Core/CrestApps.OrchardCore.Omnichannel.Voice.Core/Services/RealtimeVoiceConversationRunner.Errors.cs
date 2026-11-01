using CrestApps.Core.AI.Realtime;
using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What a live call does with an error its session reports.
/// </summary>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// How many errors in a row, with nothing else from the session between them, mean the session is gone even
    /// though none of them said so.
    /// </summary>
    internal const int MaximumConsecutiveErrors = 3;

    /// <summary>
    /// Decides whether the session carries on after an error, and repairs what the error was about when it can.
    /// </summary>
    /// <remarks>
    /// The session's set-up -- the profile's instructions, its tools and its voice -- reaches the provider as one
    /// message, and the provider refuses that message whole over a single value it does not accept. Its answer comes
    /// before anything else the session sends, so an unexpected error with nothing from the session before it is
    /// that refusal, and what would carry on is a model that knows nothing of the call. Live, OpenAI refused a voice
    /// of another provider ("Invalid value: '...'. Supported values are: 'alloy', ..."), and the model went on to tell
    /// the caller it had booked an appointment, with no tool to book it.
    /// </remarks>
    /// <param name="conversation">The session that reported the error.</param>
    /// <param name="bargeIn">What is queued on the line.</param>
    /// <param name="message">The provider's error message.</param>
    /// <param name="consecutiveErrors">How many errors in a row the session has reported, this one included.</param>
    /// <param name="heardFromSession">Whether the session has sent anything other than errors before this one.</param>
    /// <param name="activityId">The call's activity, for the log.</param>
    /// <param name="cancellationToken">The call's token.</param>
    /// <returns><see langword="true"/> when the session carries on; <see langword="false"/> when it is lost.</returns>
    private async Task<bool> RideOutErrorAsync(
        IRealtimeConversation conversation,
        AssistantBargeIn bargeIn,
        string message,
        int consecutiveErrors,
        bool heardFromSession,
        string activityId,
        CancellationToken cancellationToken)
    {
        var kind = RealtimeSessionErrors.Classify(message);
        var sanitized = message.SanitizeLogValue();

        if (kind == RealtimeSessionErrorKind.Unexpected && !heardFromSession)
        {
            _meter?.Failed();
            _logger.LogError(
                "The realtime voice session on activity '{ActivityId}' refused its set-up, so it has neither the profile's instructions nor its tools, and is not carried on: {Error}",
                activityId,
                sanitized);

            return false;
        }

        if (kind == RealtimeSessionErrorKind.Fatal ||
            (kind == RealtimeSessionErrorKind.Unexpected && consecutiveErrors >= MaximumConsecutiveErrors))
        {
            _meter?.Failed();
            _logger.LogError(
                "A realtime voice session reported an error on activity '{ActivityId}' that it cannot continue from: {Error}",
                activityId,
                sanitized);

            return false;
        }

        _logger.LogWarning(
            "A realtime voice session reported an error on activity '{ActivityId}' and carries on: {Error}",
            activityId,
            sanitized);

        if (RealtimeSessionErrors.TryReadTruncationRefusal(message, out var heldMilliseconds, out var requestedMilliseconds))
        {
            await CutBackWithinWhatTheProviderHoldsAsync(conversation, bargeIn, heldMilliseconds, requestedMilliseconds, activityId, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Sends a refused cut again, scaled into the audio the provider says the line really holds.
    /// </summary>
    /// <remarks>
    /// The provider can count less audio for a line than was delivered for it: live, it held 5 150 ms of a line
    /// this side had received more than 5 300 ms of. A cut measured against what arrived can then land past the end of the line as the
    /// provider sees it, and is refused. Left there, the model believes the caller heard every word of a line
    /// they talked over. The refusal quotes the provider's length, so the cut is put at the same share of it.
    /// </remarks>
    private async Task CutBackWithinWhatTheProviderHoldsAsync(
        IRealtimeConversation conversation,
        AssistantBargeIn bargeIn,
        int heldMilliseconds,
        int requestedMilliseconds,
        string activityId,
        CancellationToken cancellationToken)
    {
        if (!bargeIn.TryTakeRefused(requestedMilliseconds, out var refused) || refused.DeliveredMilliseconds <= 0)
        {
            return;
        }

        var corrected = (int)Math.Min(
            heldMilliseconds,
            (long)heldMilliseconds * refused.AudioEndMilliseconds / refused.DeliveredMilliseconds);

        try
        {
            await conversation.TruncateAssistantAudioAsync(refused.ItemId, corrected, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "The provider on activity '{ActivityId}' holds {HeldMilliseconds} ms of a line cut at {RequestedMilliseconds} ms of {DeliveredMilliseconds} ms delivered; cut it at {CorrectedMilliseconds} ms instead.",
                    activityId,
                    heldMilliseconds,
                    requestedMilliseconds,
                    refused.DeliveredMilliseconds,
                    corrected);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not cut back the assistant's line on activity '{ActivityId}' after the provider refused the first cut.", activityId);
        }
    }
}
