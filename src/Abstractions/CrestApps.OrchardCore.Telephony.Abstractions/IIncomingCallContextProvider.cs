using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Contributes contextual cards shown alongside a ringing inbound call in the soft-phone
/// incoming-call modal. Other modules implement this contract to surface related records, such as a
/// Contact Center showing customers matched by the caller's phone number, without the Telephony
/// module taking a dependency on them.
/// </summary>
public interface IIncomingCallContextProvider
{
    /// <summary>
    /// Contributes cards for the ringing inbound call described by the supplied context.
    /// </summary>
    /// <param name="context">The contribution context that carries the call and accepts the cards.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ContributeAsync(IncomingCallContributionContext context, CancellationToken cancellationToken = default);
}
