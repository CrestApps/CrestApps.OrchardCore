using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Told when a call in a user's telephony history starts and when it ends: an extension call to or from a
/// colleague, or a call the soft phone placed straight from the browser.
/// </summary>
/// <remarks>
/// Implemented by whatever keeps a record of the time people spend on calls, such as the contact center, which
/// telephony knows nothing of. The history store tells every registered observer each time it saves a call that
/// has started or ended, so an observer sees the same call more than once and must treat a repeat as the same
/// change. An observer must not throw: the call is already recorded, and a record of it elsewhere must not undo
/// that.
/// </remarks>
public interface ITelephonyCallObserver
{
    /// <summary>
    /// Records that a call started.
    /// </summary>
    /// <param name="interaction">The call as it was saved.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task CallStartedAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a call ended.
    /// </summary>
    /// <param name="interaction">The call as it was saved, with its outcome and end time.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task CallEndedAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default);
}
