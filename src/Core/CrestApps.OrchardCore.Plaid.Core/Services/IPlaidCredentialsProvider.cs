using CrestApps.OrchardCore.Plaid.Models;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Reads the site's Plaid credentials: the environment and client id from the settings, the secret from the Orchard
/// secret the settings name.
/// </summary>
public interface IPlaidCredentialsProvider
{
    /// <summary>
    /// Returns the credentials, or <see langword="null"/> when Plaid is not set up or the secret can't be read.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The credentials, or <see langword="null"/>.</returns>
    Task<PlaidCredentials> GetAsync(CancellationToken cancellationToken = default);
}
