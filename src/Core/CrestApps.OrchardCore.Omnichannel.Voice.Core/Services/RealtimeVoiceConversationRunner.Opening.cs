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

    // One while the call is still on the opening detector.
    private int _openingDetector;

    private async Task ApplyOpeningTurnDetectionAsync(IRealtimeConversation conversation, CancellationToken cancellationToken)
    {
        try
        {
            await conversation.UpdateTurnDetectionAsync(
                allowInterruption: true,
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
}
