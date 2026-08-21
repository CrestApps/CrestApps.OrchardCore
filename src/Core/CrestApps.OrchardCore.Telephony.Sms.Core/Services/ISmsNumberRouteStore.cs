using CrestApps.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The persistence contract for <see cref="SmsNumberRoute"/>.
/// </summary>
public interface ISmsNumberRouteStore : ICatalog<SmsNumberRoute>
{
    /// <summary>
    /// Finds the enabled route bound to the specified dialed number (DID).
    /// </summary>
    /// <param name="dialedNumber">The DID the inbound message was received on.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching enabled route, or <see langword="null"/> when none exists.</returns>
    Task<SmsNumberRoute> FindByDialedNumberAsync(string dialedNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the routes bound to the specified endpoint (DID).
    /// </summary>
    /// <param name="endpointId">The endpoint identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The routes bound to the endpoint.</returns>
    Task<IReadOnlyCollection<SmsNumberRoute>> GetByEndpointAsync(string endpointId, CancellationToken cancellationToken = default);
}
