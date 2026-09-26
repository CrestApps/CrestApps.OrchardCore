using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Stopping the assistant when the caller talks over it.
/// </summary>
/// <remarks>
/// Split from the pumps because it is the one place both of them meet, and because the pump file is near the size
/// the architecture guard allows.
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// Takes the assistant's queued speech off the line when the caller has started talking over it, and tells the
    /// model how much of its line was heard.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Interruption is on for the session, so the provider stops the model the moment it hears the caller start.
    /// That is not the same as the caller no longer hearing it. The model produces speech faster than it plays, so
    /// by the time a caller talks over a sentence most of it is already queued at the carrier. Live, a caller talked
    /// over the assistant three times on one call — the echo guard let every one of them through — and heard it
    /// finish its sentence each time. So the queue is cleared, the model's item is cut back to what was actually
    /// played so its context matches what the caller heard, and anything more of that line that arrives late is
    /// dropped.
    /// </para>
    /// <para>
    /// It acts only on a real interruption: the provider reporting speech <em>and</em> the echo guard having let a
    /// voice through over the assistant. The provider's report alone is not enough, because its detector also
    /// fires on the assistant's own echo, and cutting the assistant off on that is the assistant interrupting
    /// itself. A cough loud enough to pass the guard is not enough either, because the provider never reports it
    /// and so never stops the model; clearing the line on it would leave the assistant silent mid-sentence with
    /// nothing coming to answer.
    /// </para>
    /// <para>
    /// The closing line is deliberately left to finish — the goodbye, or the line announcing a transfer. It is
    /// short, goodbyes overlap on a phone ("thanks, bye" said over the assistant's own), and clipping it is exactly
    /// what the closing path exists to prevent. The caller still takes the call back: speaking abandons the hangup,
    /// and the model answers once the line has played.
    /// </para>
    /// </remarks>
    /// <param name="media">The call's media session.</param>
    /// <param name="conversation">The live session.</param>
    /// <param name="bargeIn">What is queued on the line, and whether the caller is talking over it.</param>
    /// <param name="closing">Whether what is playing is the closing line.</param>
    /// <param name="activityId">The activity, already sanitized for logging.</param>
    /// <param name="cancellationToken">The call's token.</param>
    private async Task StopSpeakingWhenTalkedOverAsync(
        IContactCenterVoiceMediaSession media,
        IRealtimeConversation conversation,
        AssistantBargeIn bargeIn,
        bool closing,
        string activityId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow.Ticks;
        var playsUntil = Interlocked.Read(ref _assistantSpeechEndsTicks);

        // Nothing is left playing, so there is nothing to take back: the caller is simply answering.
        if (now >= playsUntil)
        {
            return;
        }

        var remainingMilliseconds = (playsUntil - now) / TimeSpan.TicksPerMillisecond;

        if (!bargeIn.IsCallerTalkingOver(now))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Speech was reported on activity '{ActivityId}' with {RemainingMilliseconds} ms of the assistant still to play, but the echo guard heard no voice over it, so it is treated as the assistant's own echo and the assistant keeps talking.",
                    activityId,
                    remainingMilliseconds);
            }

            return;
        }

        if (closing)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "The caller on activity '{ActivityId}' talked over the closing line with {RemainingMilliseconds} ms of it still to play; it is allowed to finish.",
                    activityId,
                    remainingMilliseconds);
            }

            return;
        }

        var truncations = bargeIn.Interrupt(now);
        _meter?.AssistantInterrupted(now);

        // The line is quiet from now, not from when the queued speech would have ended: the bed comes back, the
        // idle watchdog counts from here, and the assistant's answer plays straight away instead of after audio
        // nobody will hear.
        Interlocked.Exchange(ref _assistantSpeechEndsTicks, now);
        Interlocked.Exchange(ref _lastAssistantAudioTicks, now);

        // What had not reached the line yet goes with it, and whatever is said next fades in rather than starting
        // on a step from the silence the clear leaves behind.
        _outgoing?.Reset();

        try
        {
            await media.ClearOutgoingAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The caller hears the rest of the line, as they did before this existed. The call itself is fine.
            _logger.LogWarning(ex, "Could not clear the assistant's queued speech on activity '{ActivityId}' after the caller talked over it.", activityId);
        }

        foreach (var truncation in truncations)
        {
            // The opening is taken off the line but never cut back. Cutting a line back deletes the provider's text
            // of it, and for the opening that text is the model's only record of having introduced itself: twice
            // live, a caller who talked over it was greeted again from the top, four times in fifteen seconds.
            if (bargeIn.IsOpeningLine(truncation.ItemId))
            {
                continue;
            }

            try
            {
                await conversation.TruncateAssistantAudioAsync(truncation.ItemId, truncation.AudioEndMilliseconds, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The model believes the caller heard all of the line. Worth knowing, not worth ending the call.
                _logger.LogWarning(ex, "Could not tell the realtime session on activity '{ActivityId}' how much of the assistant's line was heard.", activityId);
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The caller on activity '{ActivityId}' talked over the assistant, so its speech was stopped with {RemainingMilliseconds} ms still to play and {Truncated} line(s) cut back to what was heard ({HeardMilliseconds} ms).",
                activityId,
                remainingMilliseconds,
                truncations.Count(truncation => !bargeIn.IsOpeningLine(truncation.ItemId)),
                truncations.Count > 0 ? truncations[0].AudioEndMilliseconds : 0);
        }
    }
}
