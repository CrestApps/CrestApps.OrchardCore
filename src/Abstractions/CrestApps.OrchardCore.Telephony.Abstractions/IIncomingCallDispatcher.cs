using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Pushes a ringing inbound call to a specific user's connected soft phone. The dispatcher gathers
/// the contextual cards from the registered <see cref="IIncomingCallContextProvider"/> instances and
/// raises the incoming-call modal on every soft-phone connection the user currently has open.
/// </summary>
public interface IIncomingCallDispatcher
{
    /// <summary>
    /// Offers the ringing inbound call to the specified user's soft phone.
    /// </summary>
    /// <param name="userId">The identifier of the user the call is offered to.</param>
    /// <param name="call">The ringing inbound call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DispatchAsync(string userId, TelephonyCall call, CancellationToken cancellationToken = default);
}
