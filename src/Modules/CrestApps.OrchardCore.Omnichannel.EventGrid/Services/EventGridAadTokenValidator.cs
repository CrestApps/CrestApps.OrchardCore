using System.Collections.Concurrent;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Models;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CrestApps.OrchardCore.Omnichannel.EventGrid.Services;

/// <summary>
/// Validates Event Grid's Microsoft Entra ID bearer tokens for one tenant, keeping one configuration manager per
/// metadata address, which caches the metadata and signing keys and refreshes them on its own schedule. A tenant
/// singleton, so the cache goes with the tenant when it is released rather than living for the whole process.
/// </summary>
internal sealed class EventGridAadTokenValidator
{
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns whether the token was issued by the configured issuer, for the configured audience, is within its lifetime
    /// and is signed with one of the issuer's keys.
    /// </summary>
    /// <param name="token">The bearer token.</param>
    /// <param name="options">The Event Grid options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<bool> ValidateAsync(string token, EventGridOptions options, CancellationToken cancellationToken)
        => EventGridAadTokens.ValidateAsync(token, options, _managers, _ => new HttpDocumentRetriever { RequireHttps = true }, cancellationToken);
}
