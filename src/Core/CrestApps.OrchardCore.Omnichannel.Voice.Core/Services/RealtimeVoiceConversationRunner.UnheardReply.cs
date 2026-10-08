using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// How long after the caller's reply the provider is given to report hearing it before the assistant asks for
    /// it again. The provider reports speech a few hundred milliseconds after it begins, so a reply it has not
    /// reported by now is one it is not going to. Two seconds rather than one: live, at one and a bit, a soft "um"
    /// before an answer was taken for a missed reply and the question came just as the caller began to answer.
    /// The reply this exists for went unanswered for seven.
    /// </summary>
    private static readonly TimeSpan UnheardReplyWait = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How often the unheard-reply watchdog looks. Fine enough that the assistant asks within a breath.
    /// </summary>
    private static readonly TimeSpan UnheardReplyPollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// How many times on one call the assistant asks for a reply it did not catch. A line that keeps producing
    /// voice the provider will not hear is noise, and asking about it again and again is worse than the idle prompt.
    /// </summary>
    private const int MaximumUnheardReplyPrompts = 3;

    /// <summary>
    /// How long a caller turn the provider opened and never committed is still taken as the provider listening. A
    /// turn is normally committed within seconds; one that never is was dropped, and must not silence this for good.
    /// </summary>
    private static readonly TimeSpan OpenCallerTurnLimit = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Listens for the caller answering after the assistant has finished; see <see cref="CallerReplyListener"/>.
    /// </summary>
    private CallerReplyListener _replyListener;

    // The assistant's last finished line, for the prompt that asks for an answer it did not hear.
    private string _lastAssistantLine;

    /// <summary>
    /// When the provider last reported anything of the caller's turn: its start, or its being committed.
    /// </summary>
    private long _providerHeardCallerTicks;

    /// <summary>
    /// When the provider opened a caller turn it has not committed yet, or zero when none is open.
    /// </summary>
    /// <remarks>
    /// While a turn is open the provider is listening to the caller, however long ago it started. Live, a caller who
    /// talked over the assistant kept going for five seconds after the assistant stopped; the provider had started
    /// their turn before that, and what they said after the assistant stopped was taken for a reply it never heard.
    /// </remarks>
    private long _callerTurnOpenSinceTicks;

    /// <summary>
    /// One while the model is producing a response, so nothing asks it for another on top.
    /// </summary>
    private int _responseInFlight;

    /// <summary>
    /// Asks the caller to say it again when they answered and the provider never heard it.
    /// </summary>
    /// <remarks>
    /// The model cannot answer a reply that never reached it, so this cannot make it hear the "yes". What it can do
    /// is what a person on a bad line does — "sorry, was that a yes?" — within a couple of seconds, instead of the
    /// caller sitting in a silence they cannot explain until the idle prompt or their own patience breaks it.
    /// </remarks>
    private async Task AskAgainWhenAReplyGoesUnheardAsync(
        LiveConversation live,
        RealtimeVoiceConversationContext context,
        CancellationToken callToken)
    {
        var attempts = 0;

        try
        {
            while (!callToken.IsCancellationRequested && attempts < MaximumUnheardReplyPrompts)
            {
                await Task.Delay(UnheardReplyPollInterval, callToken);

                var listener = _replyListener;

                if (listener is null)
                {
                    continue;
                }

                var now = DateTime.UtcNow.Ticks;

                if (!listener.TryTakeUnheardReply(now, ProviderHeardCallerTicks(now), UnheardReplyWait))
                {
                    continue;
                }

                // The call is closing, and a "thanks, bye" the provider missed needs no answer. A lost session is
                // being replaced, and the replacement speaks first anyway.
                var conversation = live.Current;

                // A response under way is the model answering something, and asking it for another fails anyway.
                if (context.EndCallRequested.IsCancellationRequested ||
                    context.HandoffRequested.IsCancellationRequested ||
                    conversation is null ||
                    Volatile.Read(ref _responseInFlight) == 1 ||
                    now < Interlocked.Read(ref _lastAssistantAudioTicks))
                {
                    continue;
                }

                attempts++;

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "The caller on activity '{ActivityId}' answered and the realtime session never reported hearing it, so the assistant is asking them to say it again (attempt {Attempt}).",
                        context.Activity?.ItemId.SanitizeLogValue(),
                        attempts);
                }

                // Stamped so the idle prompt measures from this, rather than asking a second time on top of it.
                Interlocked.Exchange(ref _lastAssistantAudioTicks, now);

                await conversation.RequestUnpromptedResponseAsync(
                    WithSessionInstructions(UnheardReplyPrompt(Volatile.Read(ref _lastAssistantLine))),
                    callToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The call ended, which is the ordinary way this stops.
        }
        catch (Exception ex)
        {
            // A prompt that cannot be sent must not take the call down with it.
            _logger.LogWarning(ex, "Could not ask the caller to repeat an answer the realtime session did not hear.");
        }
    }

    /// <summary>
    /// Says how loud the model spoke on this call and what the leveler did about it, so a model that sounds wrong on
    /// the phone can be checked against numbers rather than recordings.
    /// </summary>
    /// <param name="context">The call.</param>
    private void LogVoiceLevel(RealtimeVoiceConversationContext context)
    {
        var leveler = _outgoing?.Leveler;

        if (leveler is null || leveler.SpeechMilliseconds == 0 || !_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        _logger.LogInformation(
            "The assistant on activity '{ActivityId}' spoke at {SpeechLevelDbfs:F1} dBFS over {SpeechSeconds:F1} s of speech; its speech was raised by {AverageGainDb:F1} dB on average toward {TargetDbfs:F1} dBFS, and {LimitedSamples} samples were turned down so as not to clip.",
            context.Activity?.ItemId.SanitizeLogValue(),
            leveler.SpeechLevelDbfs,
            leveler.SpeechMilliseconds / 1000d,
            leveler.AverageSpeechGainDb,
            AssistantVoiceLeveler.TargetDbfs,
            leveler.LimitedSamples);
    }

    /// <summary>
    /// What the model is asked to say when the caller's answer went unheard.
    /// </summary>
    /// <remarks>
    /// It is told what it last said. Asked only to "say it again", a model that had just read an email address back
    /// and asked "did I get that right?" asked the caller for the whole address again, and the caller -- who had
    /// said "yes" -- answered that they had already given it and it had already been confirmed.
    /// </remarks>
    /// <param name="lastAssistantLine">The assistant's last line, or <see langword="null"/> when there is none.</param>
    internal static string UnheardReplyPrompt(string lastAssistantLine)
    {
        var asked = string.IsNullOrWhiteSpace(lastAssistantLine)
            ? string.Empty
            : $"Your last words to them were: \"{lastAssistantLine.Trim()}\" ";

        return
            "The customer just answered you, but their answer was too short or too faint for you to hear. " + asked +
            "Say one short sentence only, asking them to repeat their answer to that -- after a yes-or-no question, " +
            "simply \"Sorry, was that a yes?\". Do not ask them to repeat anything they told you earlier, do not repeat " +
            "your whole question, do not ask anything new, and do not move on.";
    }

    /// <summary>
    /// Records the provider reporting the caller's turn: started (and still open) or committed.
    /// </summary>
    /// <param name="turnOpen">Whether the turn is still open, as it is when the provider has only heard it start.</param>
    private void ProviderHeardCaller(bool turnOpen)
    {
        var now = DateTime.UtcNow.Ticks;
        Interlocked.Exchange(ref _providerHeardCallerTicks, now);

        if (!turnOpen)
        {
            Interlocked.Exchange(ref _callerTurnOpenSinceTicks, 0);
        }
        else if (Interlocked.Read(ref _callerTurnOpenSinceTicks) == 0)
        {
            Interlocked.Exchange(ref _callerTurnOpenSinceTicks, now);
        }
    }

    private void ResponseInFlight(bool inFlight)
        => Interlocked.Exchange(ref _responseInFlight, inFlight ? 1 : 0);

    /// <summary>
    /// When the provider last heard the caller, counting a turn it still has open as hearing them now.
    /// </summary>
    /// <param name="nowTicks">The current time, in UTC ticks.</param>
    private long ProviderHeardCallerTicks(long nowTicks)
    {
        var openSince = Interlocked.Read(ref _callerTurnOpenSinceTicks);

        if (openSince > 0 && nowTicks - openSince < OpenCallerTurnLimit.Ticks)
        {
            return nowTicks;
        }

        return Interlocked.Read(ref _providerHeardCallerTicks);
    }
}
