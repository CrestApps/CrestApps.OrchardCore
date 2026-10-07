using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Tells the soft phone of the user a call belongs to where the call's other party stands.
/// </summary>
/// <remarks>
/// A provider that connects a soft-phone call on the server -- the agent's own leg is answered first and the number is
/// dialed afterwards -- uses this to say when the number starts ringing, answers, or goes, which the agent's leg never
/// reports. The call is named by the provider's identifier for the agent's leg, the one the soft phone tracks. A
/// notifier must not throw for a call it does not know: the call is unaffected either way.
/// </remarks>
public interface ITelephonyRemotePartyNotifier
{
    /// <summary>
    /// Tells the user of a call where its other party stands.
    /// </summary>
    /// <param name="providerName">The technical name of the provider the call is on.</param>
    /// <param name="providerCallId">The provider's identifier of the agent's leg, as the soft phone tracks the call.</param>
    /// <param name="state">Where the other party stands.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task NotifyAsync(string providerName, string providerCallId, RemotePartyState state, CancellationToken cancellationToken = default);
}
