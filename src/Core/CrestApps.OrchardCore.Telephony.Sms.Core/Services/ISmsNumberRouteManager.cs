using CrestApps.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The management contract for <see cref="SmsNumberRoute"/>.
/// </summary>
public interface ISmsNumberRouteManager : ICatalogManager<SmsNumberRoute>
{
    /// <summary>
    /// Finds the enabled route bound to the specified dialed number (DID).
    /// </summary>
    /// <param name="dialedNumber">The DID the inbound message was received on.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching enabled route, or <see langword="null"/> when none exists.</returns>
    Task<SmsNumberRoute> FindByDialedNumberAsync(string dialedNumber, CancellationToken cancellationToken = default);
}
