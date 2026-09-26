using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Webhooks the platform asked to hear about through a call-flow <c>client_state</c>: the leg an external transfer
/// rang, and the end of a last message after which the call is hung up.
/// </summary>
public sealed partial class TelnyxWebhookService
{
    private const string SpeakEndedEventType = "call.speak.ended";

    /// <summary>
    /// Handles the event when it belongs to a call-flow state, or returns <see langword="null"/> for the ordinary
    /// pipeline to process.
    /// </summary>
    private async Task<TelnyxWebhookResult?> HandleCallFlowAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken)
    {
        if (!TelnyxCallFlowClientState.TryParse(callEvent.ClientState, out var state))
        {
            return null;
        }

        var eventType = callEvent.EventType?.Trim().ToLowerInvariant();

        if (state.Intent == TelnyxCallFlowClientState.TransferLegIntent)
        {
            return await HandleTransferLegAsync(callEvent, state, eventType, cancellationToken);
        }

        // Telnyx carries a command's client_state on every later webhook for the leg, so the caller's own hang-up
        // arrives with this state too. Only the end of the message is acted on; everything else goes on as usual.
        if (eventType != SpeakEndedEventType || string.IsNullOrEmpty(callEvent.CallControlId))
        {
            return null;
        }

        // Hanging up a leg that has already ended is harmless; the caller may well have gone first.
        await _apiClient.HangupAsync(callEvent.CallControlId, cancellationToken);

        return TelnyxWebhookResult.Updated;
    }

    // The leg a transfer created belongs to nothing the platform tracks: it only says whether the transfer worked.
    // Telnyx documents that an unsuccessful transfer sends call.hangup for that leg and leaves the caller's leg up for
    // further commands (https://developers.telnyx.com/api-reference/call-commands/transfer-call).
    private async Task<TelnyxWebhookResult> HandleTransferLegAsync(
        TelnyxCallEvent callEvent,
        TelnyxCallFlowClientState state,
        string eventType,
        CancellationToken cancellationToken)
    {
        var answered = eventType is "call.answered" or "call.bridged";

        if (!answered && eventType != "call.hangup")
        {
            return TelnyxWebhookResult.Ignored;
        }

        var cause = callEvent.HangupCause?.Trim().ToLowerInvariant();
        var outcome = new ExternalTransferOutcome
        {
            ProviderName = TelnyxConstants.ProviderTechnicalName,
            InteractionId = state.InteractionId,
            ProviderLegId = callEvent.CallControlId,
            Answered = answered,

            // The caller hanging up while the destination rang cancels the destination leg; nobody is left to put
            // anywhere else.
            CallerLeft = !answered && cause is "originator_cancel",
            HangupCause = callEvent.HangupCause,
            DeliveryId = callEvent.EventId,
        };

        var handled = await _transferOutcomeSink.HandleAsync(outcome, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The external transfer leg '{LegId}' for interaction '{InteractionId}' reported {EventType} (cause '{Cause}'); applied: {Handled}.",
                callEvent.CallControlId.SanitizeLogValue(),
                state.InteractionId.SanitizeLogValue(),
                eventType.SanitizeLogValue(),
                callEvent.HangupCause.SanitizeLogValue(),
                handled);
        }

        return handled ? TelnyxWebhookResult.Routed : TelnyxWebhookResult.Ignored;
    }
}
