using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Told when an agent puts a call on hold from the soft phone, or takes it off hold.
/// </summary>
/// <remarks>
/// Some providers perform hold in the agent's own media -- the browser swaps the microphone for hold audio -- so the
/// provider never reports the hold and nothing on the server would otherwise know it happened. The soft phone's hub
/// tells every registered observer once the provider accepted the command, dated by when the agent asked, so a
/// record of the time a customer spent on hold, such as the contact center's, can be kept. A client that retries the
/// command reports the same change again, so an observer must treat a repeat as the same change. An observer must not
/// throw: the call is already held or resumed, and a failure to record it must not fail the agent's command.
/// </remarks>
public interface ITelephonyCallHoldObserver
{
    /// <summary>
    /// Records that the agent put a call on hold or took it off hold.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task CallHoldChangedAsync(TelephonyCallHoldChange change, CancellationToken cancellationToken = default);
}
