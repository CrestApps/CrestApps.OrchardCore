using CrestApps.OrchardCore.ContactCenter.Services;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The end of a digit collection: what a caller pressed on a phone menu, handed to the Contact Center in the
/// provider-neutral shape it applies to the menu.
/// </summary>
public sealed partial class TelnyxWebhookService
{
    private async Task<TelnyxWebhookResult> HandleGatherEndedAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(callEvent.CallControlId))
        {
            return TelnyxWebhookResult.Ignored;
        }

        var handled = await _digitsSink.HandleDigitsAsync(new InboundVoiceDigitsEvent
        {
            ProviderName = TelnyxConstants.ProviderTechnicalName,
            ProviderCallId = callEvent.CallControlId,
            Digits = callEvent.Digits,
            DeliveryId = callEvent.EventId,
            Outcome = MapGatherStatus(callEvent.GatherStatus, callEvent.Digits),
        }, cancellationToken);

        return handled ? TelnyxWebhookResult.Routed : TelnyxWebhookResult.Ignored;
    }

    /// <summary>
    /// Maps Telnyx's <c>status</c> on <c>call.gather.ended</c> (<c>valid</c>, <c>invalid</c>, <c>call_hangup</c>,
    /// <c>cancelled</c>, <c>cancelled_amd</c>, <c>timeout</c>) to how the collection ended.
    /// </summary>
    /// <param name="status">The status Telnyx reported.</param>
    /// <param name="digits">The digits Telnyx reported, used when the status is missing.</param>
    private static InboundVoiceDigitsOutcome MapGatherStatus(string status, string digits)
        => status?.Trim().ToLowerInvariant() switch
        {
            "valid" => InboundVoiceDigitsOutcome.Collected,
            "invalid" => InboundVoiceDigitsOutcome.Invalid,
            "timeout" => InboundVoiceDigitsOutcome.TimedOut,
            "call_hangup" => InboundVoiceDigitsOutcome.CallerHungUp,
            "cancelled" or "cancelled_amd" => InboundVoiceDigitsOutcome.Cancelled,

            // An older or unexpected payload without a status is read from its digits, which is all it says.
            _ => string.IsNullOrWhiteSpace(digits) ? InboundVoiceDigitsOutcome.TimedOut : InboundVoiceDigitsOutcome.Collected,
        };
}
