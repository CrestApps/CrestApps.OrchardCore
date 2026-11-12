using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Models;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CrestApps.OrchardCore.Omnichannel.EventGrid.Services;

/// <summary>
/// Validates the Microsoft Entra ID bearer token Event Grid sends. The endpoint is anonymous, so nothing a caller sends
/// may cost an outbound request: a token that isn't a JWT is refused before anything is fetched, the metadata address
/// must be HTTPS, and one configuration manager is kept per metadata address, which caches the metadata and signing keys
/// and refreshes them on its own schedule.
/// </summary>
internal static class EventGridAadTokens
{
    // The metadata and signing keys are public documents, the same for every tenant that names the address.
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns whether the token was issued by the configured issuer, for the configured audience, is within its lifetime
    /// and is signed with one of the issuer's keys.
    /// </summary>
    /// <param name="token">The bearer token.</param>
    /// <param name="options">The Event Grid options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static Task<bool> ValidateAsync(string token, EventGridOptions options, CancellationToken cancellationToken)
        => ValidateAsync(token, options, _managers, _ => new HttpDocumentRetriever { RequireHttps = true }, cancellationToken);

    internal static async Task<bool> ValidateAsync(
        string token,
        EventGridOptions options,
        ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> managers,
        Func<string, IDocumentRetriever> createRetriever,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var handler = new JwtSecurityTokenHandler();

        if (string.IsNullOrWhiteSpace(token) || !handler.CanReadToken(token))
        {
            return false;
        }

        if (!Uri.TryCreate(options.AADMetadataAddress, UriKind.Absolute, out var metadataAddress) || metadataAddress.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var configurationManager = managers.GetOrAdd(metadataAddress.AbsoluteUri, address => new ConfigurationManager<OpenIdConnectConfiguration>(
            address,
            new OpenIdConnectConfigurationRetriever(),
            createRetriever(address)));
        var openIdConfiguration = await configurationManager.GetConfigurationAsync(cancellationToken);
        var validationResult = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.AADIssuer,
            ValidateAudience = true,
            ValidAudience = options.AADAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = openIdConfiguration.SigningKeys,
        });

        return validationResult.IsValid;
    }
}
