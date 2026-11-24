using CrestApps.OrchardCore.Plaid.Models;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Gives the tenant's Plaid credentials, the one Plaid setup every module of the tenant shares. By default the Plaid
/// feature reads them from the site's Plaid settings (the environment and client id) and the Orchard secret the settings
/// name. A host that holds the keys itself, such as one set of platform keys for every tenant, registers its own
/// provider with <see cref="PlaidServiceCollectionExtensions.AddPlaidCredentialsProvider{TProvider}"/>, which replaces
/// the settings-based one.
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
