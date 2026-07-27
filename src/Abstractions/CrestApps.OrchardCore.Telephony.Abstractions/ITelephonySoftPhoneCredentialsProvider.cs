using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Issues the bootstrap configuration a soft phone client needs to connect to a telephony provider. A
/// provider that is driven only from the server, with no browser client, does not implement this contract.
/// </summary>
public interface ITelephonySoftPhoneCredentialsProvider
{
    /// <summary>
    /// Issues the bootstrap configuration a soft phone client needs to connect to the provider.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The <see cref="TelephonyClientCredentials"/> for the provider.</returns>
    Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default);
}
