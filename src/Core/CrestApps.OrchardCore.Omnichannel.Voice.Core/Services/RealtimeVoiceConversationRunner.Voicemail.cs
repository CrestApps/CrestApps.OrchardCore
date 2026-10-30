using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What a live call does when the provider reports that a voicemail or answering machine answered it.
/// </summary>
/// <remarks>
/// <para>
/// A recording sounds exactly like somebody picking up, and the model talks to it as to a person: live, the message
/// a customer's voicemail kept was the end of the assistant's opening line, and no message at all. The provider's
/// detection reaches the session on a webhook request of its own (see <see cref="IAnsweringMachineSignals"/>), and
/// from that moment the call stops being a conversation: what the model says to the greeting is kept off the line,
/// the greeting is given until the provider says it ended or the line has been quiet for a moment, the model is
/// asked for one message, and once that has played the call is ended the way the end-call tool ends it on voicemail.
/// </para>
/// <para>
/// The message is the model's own rather than the turn-based path's spoken one, because the turn-based path speaks
/// through the provider's text-to-speech in its default voice and in English. A call held in Greek, in the voice the
/// campaign chose, has to leave its message the same way.
/// </para>
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// How long the line must be quiet, once a machine has answered, before its greeting is taken as over and the
    /// message is left.
    /// </summary>
    /// <remarks>
    /// The same wait the turn-based path gives a greeting for its tone: long enough for the rest of the greeting and
    /// the tone, short enough that the recording is not mostly silence. A provider that hears the greeting end says
    /// so, and that is acted on at once; this is for the ones that do not.
    /// </remarks>
    internal static readonly TimeSpan VoicemailGreetingQuietWait = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long a greeting is given in all before the message is left over whatever is still playing.
    /// </summary>
    /// <remarks>
    /// A line that never falls quiet -- music behind the greeting, a hum above the voice level -- would otherwise
    /// keep the message waiting until the recording ran out.
    /// </remarks>
    internal static readonly TimeSpan VoicemailGreetingLimit = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long the model is given to begin the message once asked for it, before it is asked again.
    /// </summary>
    internal static readonly TimeSpan VoicemailMessageStartWait = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a response the model still has under way is waited for before the message is asked for regardless.
    /// </summary>
    private static readonly TimeSpan VoicemailResponseWait = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long, after the model asks to end a call a machine answered, the message still owed is waited for before
    /// the call is hung up without it.
    /// </summary>
    /// <remarks>
    /// A model that recognises the greeting itself leaves a message and ends the call before the greeting is over,
    /// and that message was kept off the line with its other replies to the greeting. The one asked for here is what
    /// the machine records, so the hangup waits for it -- bounded, so a message that never comes does not hold the
    /// recording open.
    /// </remarks>
    private static readonly TimeSpan VoicemailMessageLimit = TimeSpan.FromSeconds(45);

    /// <summary>
    /// How often the answering-machine watch looks.
    /// </summary>
    private static readonly TimeSpan VoicemailPollInterval = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// How many times the message is asked for before the call is ended without one.
    /// </summary>
    private const int MaximumVoicemailMessageRequests = 2;

    // One once the provider has said a machine answered. From then on the model's replies to the greeting stay off
    // the line.
    private int _machineAnswered;

    // When the machine was last heard -- its greeting on the line, or the provider reporting it speaking -- on the
    // injected clock, which is what the quiet before the message is measured on.
    private long _machineHeardTicks;

    // One once the message has been asked for: the next response the model starts is the message.
    private int _voicemailMessageRequested;

    // One once the model has begun the message, which is the one line the machine is let hear.
    private int _voicemailMessageStarted;

    // One once the message is over -- said, or given up on -- and the call is being ended.
    private int _voicemailMessageDone;

    // The assistant's first finished line on this call with any words in it, heard or not: the message is asked for
    // without the conversation, and this is what tells it the language the call was held in.
    private string _openingLine;

    /// <summary>
    /// Whether the provider has said that a machine answered this call.
    /// </summary>
    private bool OnAMachine()
        => Volatile.Read(ref _machineAnswered) == 1;

    /// <summary>
    /// Whether what the model is saying may reach the line: everything on a call a person answered, and on a machine
    /// only the message asked for once its greeting ended.
    /// </summary>
    private bool MayReachTheLine()
        => TheGuardrailsAllowTheLine() &&
            (!OnAMachine() ||
                (Volatile.Read(ref _voicemailMessageStarted) == 1 && Volatile.Read(ref _voicemailMessageDone) == 0));

    /// <summary>
    /// Whether the message owed to a machine is still to come, so the call is not ended before it.
    /// </summary>
    private bool VoicemailMessageOwed()
        => OnAMachine() && Volatile.Read(ref _voicemailMessageDone) == 0;

    /// <summary>
    /// Notes the machine being heard, so the quiet before the message is measured from its last word.
    /// </summary>
    private void HeardTheMachine()
    {
        if (OnAMachine())
        {
            Interlocked.Exchange(ref _machineHeardTicks, _clock.UtcNow.Ticks);
        }
    }

    /// <summary>
    /// Whether the response that is starting is the message asked for on a machine, which is let through to the line.
    /// </summary>
    private bool TheVoicemailMessageIsStarting()
        => OnAMachine() &&
            Volatile.Read(ref _voicemailMessageRequested) == 1 &&
            Interlocked.CompareExchange(ref _voicemailMessageStarted, 1, 0) == 0;

    /// <summary>
    /// Whether the response that just completed was the message, after which the call is over.
    /// </summary>
    private bool TheVoicemailMessageIsOver()
        => Volatile.Read(ref _voicemailMessageStarted) == 1 &&
            Interlocked.CompareExchange(ref _voicemailMessageDone, 1, 0) == 0;

    /// <summary>
    /// Leaves one message on the machine that answered, once the provider has reported one and its greeting is over.
    /// </summary>
    /// <param name="media">The call's media session.</param>
    /// <param name="live">The session carrying the call.</param>
    /// <param name="context">The call being held.</param>
    /// <param name="callToken">The call's token.</param>
    private async Task LeaveAMessageWhenAMachineAnswersAsync(
        IContactCenterVoiceMediaSession media,
        LiveConversation live,
        RealtimeVoiceConversationContext context,
        CancellationToken callToken)
    {
        // Nothing to watch for on a tenant whose provider never says who answered.
        if (!context.AnsweredByMachine.CanBeCanceled)
        {
            return;
        }

        try
        {
            using var answered = CancellationTokenSource.CreateLinkedTokenSource(context.AnsweredByMachine, callToken);

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, answered.Token);
            }
            catch (OperationCanceledException) when (context.AnsweredByMachine.IsCancellationRequested)
            {
                // The provider says a machine answered. This is the signal, not a failure.
            }

            callToken.ThrowIfCancellationRequested();

            // The assistant has already pressed keys: the machine is a phone menu it is getting through.
            if (context.PhoneMenuReached.IsCancellationRequested)
            {
                LogPhoneMenuNotVoicemail(context);

                return;
            }

            await StopTalkingToTheMachineAsync(media, live, context, callToken);

            // Keys pressed while the greeting plays make it a phone menu after all, so no message is left on it.
            using var menuOrCall = CancellationTokenSource.CreateLinkedTokenSource(context.PhoneMenuReached, callToken);

            try
            {
                await WaitForTheGreetingToEndAsync(context, menuOrCall.Token);
            }
            catch (OperationCanceledException) when (context.PhoneMenuReached.IsCancellationRequested && !callToken.IsCancellationRequested)
            {
                await CarryOnPastThePhoneMenuAsync(live, context, callToken);

                return;
            }

            await RequestTheVoicemailMessageAsync(live, context, callToken);
        }
        catch (OperationCanceledException)
        {
            // The call ended, which is the ordinary way this stops.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not leave a voicemail message on the machine that answered activity '{ActivityId}'.",
                context.Activity?.ItemId.SanitizeLogValue());
        }
    }

    /// <summary>
    /// Keeps the model's replies to the greeting off the line from now on, and takes back what it has already queued
    /// at the carrier: the machine records whatever reaches it.
    /// </summary>
    private async Task StopTalkingToTheMachineAsync(
        IContactCenterVoiceMediaSession media,
        LiveConversation live,
        RealtimeVoiceConversationContext context,
        CancellationToken callToken)
    {
        // The quiet before the message is counted from the report when nothing has been heard since.
        Interlocked.Exchange(ref _machineHeardTicks, _clock.UtcNow.Ticks);
        Volatile.Write(ref _machineAnswered, 1);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The provider reports that a machine answered activity '{ActivityId}'; the assistant's replies to its greeting are kept off the line, and one message is left once the greeting has ended.",
                context.Activity?.ItemId.SanitizeLogValue());
        }

        // As when the caller talks over the assistant: the line is quiet from now, not from when the queued speech
        // would have ended, and whatever had not reached the line yet goes with it.
        var now = DateTime.UtcNow.Ticks;
        Interlocked.Exchange(ref _assistantSpeechEndsTicks, now);
        Interlocked.Exchange(ref _assistantVoiceEndsTicks, Math.Min(Interlocked.Read(ref _assistantVoiceEndsTicks), now));
        Interlocked.Exchange(ref _lastAssistantAudioTicks, now);
        _outgoing?.Reset();

        try
        {
            await media.ClearOutgoingAsync(callToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The machine records the rest of the line, as it did before this existed. The message still follows.
            _logger.LogWarning(ex, "Could not clear the assistant's queued speech on activity '{ActivityId}' after a machine answered.", context.Activity?.ItemId.SanitizeLogValue());
        }

        // Nothing on a recording is somebody talking over the assistant: the detector is re-applied with
        // interruption off, so the tone or the tail of the greeting does not cut the message short.
        var conversation = live.Current;

        if (conversation is not null)
        {
            await ApplyTelephonyTurnDetectionAsync(conversation, callToken);
        }
    }

    /// <summary>
    /// Goes back to conversing once the machine that answered turns out to be a phone menu: what the model says reaches
    /// the line again, and the caller the menu puts the call through to may talk over it.
    /// </summary>
    /// <remarks>
    /// The provider's detection cannot tell a business's menu from a voicemail. Live, the assistant pressed the key for
    /// the person it was calling three seconds after the call was reported as a machine, and was then made to leave a
    /// message on the menu and hang up.
    /// </remarks>
    private async Task CarryOnPastThePhoneMenuAsync(
        LiveConversation live,
        RealtimeVoiceConversationContext context,
        CancellationToken callToken)
    {
        Volatile.Write(ref _machineAnswered, 0);

        LogPhoneMenuNotVoicemail(context);

        var conversation = live.Current;

        if (conversation is not null)
        {
            await ApplyTelephonyTurnDetectionAsync(conversation, callToken);
        }
    }

    private void LogPhoneMenuNotVoicemail(RealtimeVoiceConversationContext context)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The machine that answered activity '{ActivityId}' is a phone menu the assistant pressed keys on, so no voicemail message is left and the conversation carries on.",
                context.Activity?.ItemId.SanitizeLogValue());
        }
    }

    /// <summary>
    /// Returns once the greeting is over: the provider heard it end, the line has been quiet long enough for the tone
    /// to have sounded, or it has gone on for longer than any greeting does.
    /// </summary>
    private async Task WaitForTheGreetingToEndAsync(RealtimeVoiceConversationContext context, CancellationToken callToken)
    {
        var reportedTicks = _clock.UtcNow.Ticks;

        while (true)
        {
            callToken.ThrowIfCancellationRequested();

            if (context.MachineGreetingEnded.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "The provider heard the machine's greeting end on activity '{ActivityId}'.",
                        context.Activity?.ItemId.SanitizeLogValue());
                }

                return;
            }

            var now = _clock.UtcNow.Ticks;

            if (now - Interlocked.Read(ref _machineHeardTicks) >= VoicemailGreetingQuietWait.Ticks)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "The machine on activity '{ActivityId}' has been quiet for {Seconds} seconds, so its greeting is taken as over.",
                        context.Activity?.ItemId.SanitizeLogValue(),
                        (int)VoicemailGreetingQuietWait.TotalSeconds);
                }

                return;
            }

            if (now - reportedTicks >= VoicemailGreetingLimit.Ticks)
            {
                _logger.LogWarning(
                    "The machine on activity '{ActivityId}' has not fallen quiet {Seconds} seconds after it was reported, so the message is left over whatever is playing.",
                    context.Activity?.ItemId.SanitizeLogValue(),
                    (int)VoicemailGreetingLimit.TotalSeconds);

                return;
            }

            await Task.Delay(VoicemailPollInterval, callToken);
        }
    }

    /// <summary>
    /// Asks the model for the message, and ends the call without one when none comes.
    /// </summary>
    private async Task RequestTheVoicemailMessageAsync(
        LiveConversation live,
        RealtimeVoiceConversationContext context,
        CancellationToken callToken)
    {
        for (var attempt = 1; attempt <= MaximumVoicemailMessageRequests; attempt++)
        {
            // A response under way is the model still answering the greeting, and one asked for on top of it is
            // refused.
            await WaitForTheResponseInFlightAsync(callToken);

            // No session to ask while one that was lost is being replaced.
            var conversation = live.Current;

            if (conversation is null)
            {
                await Task.Delay(VoicemailPollInterval, callToken);

                continue;
            }

            // Set before the request rather than after it, so a response that starts the moment the request lands
            // is recognised as the message.
            Volatile.Write(ref _voicemailMessageRequested, 1);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Asking the model for the voicemail message to leave on activity '{ActivityId}' (attempt {Attempt}).",
                    context.Activity?.ItemId.SanitizeLogValue(),
                    attempt);
            }

            // Under the session's own instructions, so the message comes in the persona, voice and language the call
            // has been held in: see WithSessionInstructions. Standalone, so the greeting the model just heard is not
            // there to be answered and no tool call can end the response with nothing said.
            await conversation.RequestStandaloneMessageAsync(WithSessionInstructions(VoicemailPrompt(Volatile.Read(ref _openingLine))), callToken);

            var requestedTicks = _clock.UtcNow.Ticks;

            while (Volatile.Read(ref _voicemailMessageStarted) == 0 &&
                _clock.UtcNow.Ticks - requestedTicks < VoicemailMessageStartWait.Ticks)
            {
                await Task.Delay(VoicemailPollInterval, callToken);
            }

            if (Volatile.Read(ref _voicemailMessageStarted) == 1)
            {
                return;
            }
        }

        // No message came. The call is ended all the same, rather than left recording silence.
        _logger.LogWarning(
            "The model produced no voicemail message on activity '{ActivityId}' after {Attempts} request(s); the call is ended without one.",
            context.Activity?.ItemId.SanitizeLogValue(),
            MaximumVoicemailMessageRequests);

        EndTheCallOnVoicemail(context);
    }

    /// <summary>
    /// The instruction for the voicemail message, with the call's opening line quoted.
    /// </summary>
    /// <remarks>
    /// The message is asked for without the conversation, so without that line the model leaves it in the language
    /// of its instructions rather than the one the call was held in: live, a Greek call's message came in English.
    /// The line is quoted for its language only. On a call where the model waited for the other end to speak first,
    /// its first line on a machine is its reply to the greeting ("you're talking about a written message?"), and
    /// quoted as the call's opening words it could be carried on into the message.
    /// </remarks>
    /// <param name="openingLine">The assistant's first line on the call, or <see langword="null"/> before it has one.</param>
    internal static string VoicemailPrompt(string openingLine)
        => string.IsNullOrWhiteSpace(openingLine)
            ? VoiceCallGuidance.LeavingAVoicemail
            : VoiceCallGuidance.LeavingAVoicemail +
                " Leave the message in the same language as these words you said earlier on this call, quoted only to " +
                $"show that language, not as something to answer or carry on from: \"{openingLine.Trim()}\"";

    // Waits, for a while, for the model to finish whatever it is saying to the greeting.
    private async Task WaitForTheResponseInFlightAsync(CancellationToken callToken)
    {
        var sinceTicks = _clock.UtcNow.Ticks;

        while (Volatile.Read(ref _responseInFlight) == 1 && _clock.UtcNow.Ticks - sinceTicks < VoicemailResponseWait.Ticks)
        {
            await Task.Delay(VoicemailPollInterval, callToken);
        }
    }

    /// <summary>
    /// Ends the call the way the end-call tool does on voicemail: the message -- or the silence where one should have
    /// been -- is the last thing the recording hears, and nobody is left a moment to answer it.
    /// </summary>
    private void EndTheCallOnVoicemail(RealtimeVoiceConversationContext context)
    {
        Volatile.Write(ref _voicemailMessageDone, 1);

        // The message is the goodbye, so the closing watchdog does not wait for one to begin.
        Volatile.Write(ref _goodbyeAlreadySaid, true);

        context.EndCallOnVoicemail?.Invoke();
    }
}
