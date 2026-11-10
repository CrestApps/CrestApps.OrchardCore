namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// How a live call is closed once the model has said the conversation is over: after its goodbye has been heard, and
/// after the caller has had their moment to answer it.
/// </summary>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// Ends the call once the model has said the conversation is over and the goodbye has actually been said.
    /// </summary>
    /// <remarks>
    /// Three things have to be true before a call is cut, and each was learned by picturing the person holding
    /// the phone. The model has to have decided the conversation is finished. Its closing line has to have
    /// finished playing, or the caller hears "thanks for your ti-" and a dead line. And the caller has to have
    /// been given a breath to say the thing people say after goodbye — if they take it, the hangup is abandoned
    /// altogether and the assistant answers them, because a caller who is still talking has not finished the
    /// call no matter what the model concluded.
    /// </remarks>
    /// <param name="callScope">The scope whose cancellation ends the call.</param>
    /// <param name="endCallRequested">Cancelled when the model reports the conversation finished.</param>
    private async Task CloseWhenConversationEndsAsync(
        CancellationTokenSource callScope,
        Func<bool> reachedVoicemail,
        Func<int> endCallRequests,
        CancellationToken endCallRequested)
    {
        // Watched together, because a call ends for all sorts of reasons that are nothing to do with this: the
        // caller hangs up, the model asks to transfer, the session fails. Waiting on the end-call signal alone
        // meant that on every one of those calls this task simply never finished -- and the teardown waits for
        // it, so the session never returned and everything after it never ran. That is what left a caller who
        // had just been promised a person listening to nothing: the transfer was recorded and the code that
        // would have seated them in the queue was never reached.
        using var closing = CancellationTokenSource.CreateLinkedTokenSource(endCallRequested, callScope.Token);

        try
        {
            // Wait for the model to say the conversation is over. Nothing below runs on an ordinary call.
            await Task.Delay(Timeout.InfiniteTimeSpan, closing.Token);
        }
        catch (OperationCanceledException) when (endCallRequested.IsCancellationRequested)
        {
            // This is the signal, not a failure.
        }
        catch (OperationCanceledException)
        {
            // The call ended on its own. There is nothing left to close.
            return;
        }

        while (true)
        {
            if (await CloseAfterTheGoodbyeAsync(callScope, reachedVoicemail))
            {
                return;
            }

            // The customer answered the goodbye and has the call back. The model will end it again once they are
            // done, and that request has to be heard: live, the watch stopped here for good, the second goodbye
            // was said, and the line stayed open until the customer hung up on it. Requests made before the
            // customer spoke are spent, so only a newer one closes the call.
            if (endCallRequests is null)
            {
                return;
            }

            var spent = endCallRequests();

            while (endCallRequests() <= spent)
            {
                try
                {
                    await Task.Delay(ClosingPollInterval, callScope.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    // Returns whether the call was closed, or ended on its own; false when the customer spoke and took it back.
    private async Task<bool> CloseAfterTheGoodbyeAsync(CancellationTokenSource callScope, Func<bool> reachedVoicemail)
    {
        var requestedAtTicks = UtcNowTicks;
        var closingLineStarted = false;

        while (!callScope.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(ClosingPollInterval, callScope.Token);
            }
            catch (OperationCanceledException)
            {
                return true;
            }

            var now = UtcNowTicks;
            var lastAssistantTicks = Interlocked.Read(ref _lastAssistantAudioTicks);

            // Whether the call is being ended on a recording: the model said so as it ended it, or the provider said
            // a machine answered.
            var onVoicemail = reachedVoicemail?.Invoke() == true || OnAMachine();

            // The caller spoke after the model decided it was done. They get the call back: the assistant is
            // already answering them, and hanging up mid-answer would be worse than never having closed at all. A
            // recording cannot take the call back: its tone, and whatever plays after the message, is not somebody
            // speaking.
            if (!onVoicemail && Interlocked.Read(ref _lastCallerSpeechTicks) > requestedAtTicks)
            {
                return false;
            }

            // The message owed to a machine is still to be left: the model ended the call on one that was kept off
            // the line with its other replies to the greeting. Bounded, so a message that never comes does not hold
            // the recording open.
            if (VoicemailMessageOwed() && now - requestedAtTicks < VoicemailMessageLimit.Ticks)
            {
                continue;
            }

            // A goodbye the assistant pump has already marked as said counts as started: anything the model says
            // from here is suppressed, so waiting for it to begin only holds the line open on silence.
            closingLineStarted |= lastAssistantTicks > requestedAtTicks || Volatile.Read(ref _goodbyeAlreadySaid);

            // The model usually calls the tool and says its goodbye immediately after, so the silence at this
            // moment is the gap before it starts — not the end of anything. Waiting for it to speak is what keeps
            // the closing line from being cut off at the first word. A model that says nothing at all still has
            // to end the call, so the wait is bounded.
            if (!closingLineStarted && now - requestedAtTicks < ClosingSpeechStartGrace.Ticks)
            {
                continue;
            }

            // The model is still producing something -- usually the goodbye itself -- so it has not finished speaking.
            if (Volatile.Read(ref _responseInFlight) == 1 && now - requestedAtTicks < ClosingResponseLimit.Ticks)
            {
                continue;
            }

            // Quiet since the goodbye ended — and long enough that the caller has had their moment to answer it.
            // Measured from the end of the assistant's playback rather than from the tool call, so a long closing
            // line does not eat the window the caller was supposed to get. A voicemail has no caller to give it
            // to, so there the call ends as soon as the message has reached the far end.
            var afterGoodbye = onVoicemail ? VoicemailTailGrace : ClosingListeningGrace;

            // From the goodbye's last word rather than the silence it is padded with, when that word was still to be
            // heard after the request: the padding is about a second, and counted as speech it was heard as the call
            // taking too long to end.
            var lastWordTicks = Interlocked.Read(ref _assistantVoiceEndsTicks);
            var goodbyeEndsTicks = lastWordTicks > requestedAtTicks && lastWordTicks < lastAssistantTicks
                ? lastWordTicks
                : lastAssistantTicks;

            if (now - goodbyeEndsTicks < afterGoodbye.Ticks)
            {
                continue;
            }

            await callScope.CancelAsync();

            return true;
        }

        return true;
    }
}
