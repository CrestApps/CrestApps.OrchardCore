using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// How long after the caller's reply the provider is given to report hearing it before the assistant asks for
    /// it again. The provider reports speech a few hundred milliseconds after it begins; a reply it has not
    /// reported a second after it ended is one it is not going to.
    /// </summary>
    private static readonly TimeSpan UnheardReplyWait = TimeSpan.FromMilliseconds(1200);

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
    /// Listens for the caller answering after the assistant has finished; see <see cref="CallerReplyListener"/>.
    /// </summary>
    private CallerReplyListener _replyListener;

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

                if (!listener.TryTakeUnheardReply(now, Interlocked.Read(ref _lastCallerSpeechTicks), UnheardReplyWait))
                {
                    continue;
                }

                // The call is closing, and a "thanks, bye" the provider missed needs no answer. A lost session is
                // being replaced, and the replacement speaks first anyway.
                var conversation = live.Current;

                if (context.EndCallRequested.IsCancellationRequested ||
                    context.HandoffRequested.IsCancellationRequested ||
                    conversation is null ||
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
                    WithSessionInstructions(
                        "The customer just answered you, but their answer was too short or too faint for you to " +
                        "hear. Say one short sentence only, asking them to say it again -- after a yes-or-no " +
                        "question, for example, \"Sorry, was that a yes?\". Do not repeat your whole question, do " +
                        "not ask anything new, and do not move on."),
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
}
