using CrestApps.Core.Telephony.Models;

namespace CrestApps.Core.Telephony;

/// <summary>
/// Pushes what has happened to a call to whichever soft phones a user has open.
/// </summary>
/// <remarks>
/// The services that raise these are about calls, not about connections, so the hub, the transport and the
/// naming of the group a user's connections sit in all stay behind this contract.
/// </remarks>
public interface ITelephonySoftPhoneNotifier
{
    /// <summary>
    /// Tells a user's soft phones that a call is ringing for them, with the context gathered about the caller.
    /// </summary>
    /// <param name="userId">The identifier of the user being called.</param>
    /// <param name="call">The ringing call.</param>
    /// <param name="context">The contextual cards to show beside it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task NotifyIncomingCallAsync(string userId, TelephonyCall call, IncomingCallContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells a user's soft phones that a call they are on has changed state.
    /// </summary>
    /// <param name="userId">The identifier of the user on the call.</param>
    /// <param name="call">The call in its new state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task NotifyCallStateChangedAsync(string userId, TelephonyCall call, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks a user's soft phones to place a call, which is how a dial started from somewhere else on the site
    /// reaches the phone that is actually registered.
    /// </summary>
    /// <param name="userId">The identifier of the user whose soft phone should place the call.</param>
    /// <param name="request">The number to dial.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RequestDialAsync(string userId, TelephonyDialRequest request, CancellationToken cancellationToken = default);
}
