using CrestApps.OrchardCore.Plaid.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Secrets;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Reads the environment and client id from the site's <see cref="PlaidSettings"/> and the secret from the Orchard
/// secret they name. Nothing is cached beyond the request: a rotated secret is used at once.
/// </summary>
internal sealed class PlaidCredentialsProvider : IPlaidCredentialsProvider
{
    private readonly ISiteService _siteService;
    private readonly ISecretManager _secretManager;
    private readonly ILogger _logger;

    private PlaidCredentials _credentials;
    private bool _read;

    public PlaidCredentialsProvider(ISiteService siteService, ISecretManager secretManager, ILogger<PlaidCredentialsProvider> logger)
    {
        _siteService = siteService;
        _secretManager = secretManager;
        _logger = logger;
    }

    public async Task<PlaidCredentials> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_read)
        {
            return _credentials;
        }

        var settings = await _siteService.GetSettingsAsync<PlaidSettings>();

        if (settings.IsConfigured())
        {
            var secret = await _secretManager.GetSecretAsync<TextSecret>(settings.SecretName.Trim());

            if (string.IsNullOrWhiteSpace(secret?.Text))
            {
                _logger.LogWarning("The Plaid settings name the secret {SecretName}, which is missing, empty or not a text secret, so Plaid is off.", settings.SecretName);
            }
            else
            {
                _credentials = new PlaidCredentials(settings.Environment, settings.ClientId.Trim(), secret.Text.Trim());
            }
        }

        _read = true;

        return _credentials;
    }
}
