namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Counts the carriers' spam rejections of the calls placed from the account's numbers.
/// </summary>
public sealed partial class TelnyxWebhookService
{
    // Every leg is looked at, before the hidden legs return: a call dialed from the soft phone reaches the customer on
    // its hidden destination leg, and that is the leg a carrier refuses.
    private Task ObserveSpamRejectionAsync(TelnyxCallEvent callEvent)
        => _callerNumberReputation is null || !TelnyxCallerNumberReputation.IsSpamRejection(callEvent)
            ? Task.CompletedTask
            : _callerNumberReputation.ObserveAsync(callEvent);
}
