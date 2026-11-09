using CrestApps.Core.AI.Realtime;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Hearing the caller's first words quickly.
/// </summary>
/// <remarks>
/// <para>
/// People answer the phone with "hello?" -- often before the assistant has said a word, and quickly enough to
/// cancel the greeting it was about to say. The assistant then greets them in answer, but only once the session
/// decides they have finished. The default detector is patient by design (a pause for thought mid-sentence must not
/// be taken for the end of a turn), and live it took about 1.3 seconds after "hello" to decide; the greeting came
/// three seconds after the caller spoke.
/// </para>
/// <para>
/// So the call opens on a plain silence detector that ends a turn after half a second, and goes back to whatever the
/// session was configured with as soon as the caller's first turn is in. Patience matters from the second turn on,
/// when the caller is answering questions; the first is almost always "hello?" or "yes, speaking".
/// </para>
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// How long the caller must be quiet before their first turn is taken as finished.
    /// </summary>
    private const int OpeningSilenceDurationMilliseconds = 500;

    /// <summary>
    /// How long the caller must be quiet before the session treats their turn as finished. Longer than the
    /// provider default, because a person on the phone pauses mid-sentence and a call carries noise through those
    /// pauses; cutting in on them is what makes an assistant feel like it is not listening.
    /// </summary>
    /// <remarks>
    /// Only in effect when the session runs <c>server_vad</c>, which is a tenant setting. Under the default
    /// semantic detector Core drops it: see <see cref="ApplyTelephonyTurnDetectionAsync"/>.
    /// </remarks>
    private const int TelephonySilenceDurationMilliseconds = 900;

    /// <summary>
    /// How confident the detector must be that it is hearing speech.
    /// </summary>
    /// <remarks>
    /// Left at roughly the provider default on purpose. Raising it to 0.62 to suppress phantom turns seemed to
    /// suppress them, and a caller's short quiet "yeah" on the same build reached the model as a fragment that came
    /// back transcribed as "That's causing a fever somewhere." The assistant read that as a brush-off and politely
    /// ended the call on somebody who had just agreed to talk.
    /// <para>
    /// Neither observation can be pinned on this number. Like the silence above, it only applies under
    /// <c>server_vad</c>, and the tenant these calls run on configures no detector, so it gets the default semantic
    /// one, which ignores it. It stays at the default all the same: a short affirmative is the most common thing a caller
    /// says, and a detector made harder to trigger is exactly what clips one. Phantom turns are dealt with before
    /// the detector instead, by <see cref="CallerEchoGuard"/>, which leaves the caller's audio untouched whenever
    /// the assistant is not speaking.
    /// </para>
    /// </remarks>
    private const float TelephonyVadThreshold = 0.5f;

    // One while the call is still on the opening detector.
    private int _openingDetector;

    // One when the call opens with a recording disclosure, which is played to the end however the caller answers.
    private int _protectOpening;

    // One once the opening response has been generated; its speech can still be playing.
    private int _openingGenerated;

    // When the opening's speech finishes playing, once it has been generated.
    private long _openingPlaysUntilTicks;

    /// <summary>
    /// Whether the opening line is still to be heard in full and may not be talked over.
    /// </summary>
    /// <remarks>
    /// A recording disclosure has to be heard. Live, a caller's "Hello" a second into the opening stopped it after
    /// 0.8 seconds of the notice, and the model went on without giving it again.
    /// </remarks>
    /// <param name="nowTicks">The current time, in UTC ticks.</param>
    private bool OpeningMayNotBeTalkedOver(long nowTicks)
        => Volatile.Read(ref _protectOpening) == 1 &&
            (Volatile.Read(ref _openingGenerated) == 0 || nowTicks < Interlocked.Read(ref _openingPlaysUntilTicks));

    /// <summary>
    /// Notes a response finishing being generated: the first is the opening, which plays until its speech is over.
    /// </summary>
    private void OpeningResponseCompleted()
    {
        if (Volatile.Read(ref _protectOpening) == 1 && Interlocked.CompareExchange(ref _openingGenerated, 1, 0) == 0)
        {
            Interlocked.Exchange(ref _openingPlaysUntilTicks, Interlocked.Read(ref _assistantSpeechEndsTicks));
        }
    }

    private async Task ApplyOpeningTurnDetectionAsync(IRealtimeConversation conversation, CancellationToken cancellationToken)
    {
        try
        {
            await conversation.UpdateTurnDetectionAsync(
                allowInterruption: Volatile.Read(ref _protectOpening) == 0,
                silenceDurationMs: OpeningSilenceDurationMilliseconds,
                vadThreshold: TelephonyVadThreshold,
                turnDetectionType: RealtimeTurnDetectionTypes.ServerVad,
                cancellationToken);

            Interlocked.Exchange(ref _openingDetector, 1);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The configured detector is still a working one, just a slower start; fall back to it.
            _logger.LogWarning(ex, "Could not open a realtime voice session on the quick opening detector; the configured detector is used from the start.");
            await ApplyTelephonyTurnDetectionAsync(conversation, cancellationToken);
        }
    }

    // The caller's first turn is in: the rest of the call runs on the detector the session was configured with.
    private async Task LeaveOpeningTurnDetectionAsync(IRealtimeConversation conversation, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _openingDetector, 0) == 1)
        {
            await ApplyTelephonyTurnDetectionAsync(conversation, cancellationToken);
        }
    }

    /// <summary>
    /// Tells the session it is on a telephone, not a headset.
    /// </summary>
    /// <remarks>
    /// The provider's default turn detection assumes clean, close-mic audio. A call is 8 kHz, companded, and
    /// carries line noise and whatever leaks back from the far end's earpiece — so on the defaults the model
    /// decides the caller has started and stopped talking when they have done neither. Live calls showed it
    /// answering phantom turns ("Thank you." transcribed before anyone had spoken), transcribing the same
    /// utterance twice, and restarting its own question mid-sentence — which reads as the assistant talking to
    /// itself and never letting the caller speak.
    /// <para>
    /// Interruption stays on while a person is on the line: being talked over is the other half of sounding like a
    /// machine. It is turned off once the provider has said a machine answered, so the tone does not cut the
    /// voicemail message short (see the Voicemail partial).
    /// </para>
    /// <para>
    /// What this can and cannot change depends on the detector the session runs, and that is the tenant's
    /// realtime transport setting, not ours. Under the default (<c>semantic_vad</c>, eagerness <c>auto</c>) the
    /// Core conversation re-sends the semantic detector and <em>ignores</em> the silence and threshold passed here —
    /// semantic detection has neither. They only take effect on a tenant configured for <c>server_vad</c>. So on a
    /// default tenant this call keeps interruption on and nothing else; the phantom turns the comments above
    /// describe are held back by <see cref="CallerEchoGuard"/> before the audio reaches the detector at all.
    /// </para>
    /// </remarks>
    private async Task ApplyTelephonyTurnDetectionAsync(IRealtimeConversation conversation, CancellationToken cancellationToken)
    {
        try
        {
            // The detector type is left as the session already has it: the valid values belong to the provider,
            // and naming one here would be a guess that fails closed on a live call. Core offers no way to set
            // semantic eagerness or input noise reduction from here, which is why neither is tuned.
            // Interruption is off once a machine has answered: nothing on a recording is somebody talking over the
            // assistant, and its tone must not cut the one message short. See the Voicemail partial.
            await conversation.UpdateTurnDetectionAsync(
                allowInterruption: !OnAMachine(),
                silenceDurationMs: TelephonySilenceDurationMilliseconds,
                vadThreshold: TelephonyVadThreshold,
                turnDetectionType: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Tuning is an improvement, not a precondition. A provider that will not accept it still carries
            // the call on its defaults.
            _logger.LogWarning(ex, "Could not apply telephony turn detection to a realtime voice session; the provider defaults stay in effect.");
        }
    }
}
