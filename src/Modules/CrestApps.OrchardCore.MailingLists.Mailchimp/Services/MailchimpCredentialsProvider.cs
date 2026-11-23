using CrestApps.Core.Support;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Secrets;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// Reads the server prefix from the site's <see cref="MailchimpSettings"/> and the API key from the Orchard Core text
/// secret they name. Nothing is cached beyond the request, so a rotated key is used at once.
/// </summary>
internal sealed class MailchimpCredentialsProvider : IMailchimpCredentialsProvider
{
    private readonly ISiteService _siteService;
    private readonly ISecretManager _secretManager;
    private readonly ILogger _logger;

    private MailchimpCredentials _credentials;
    private bool _read;

    public MailchimpCredentialsProvider(
        ISiteService siteService,
        ISecretManager secretManager,
        ILogger<MailchimpCredentialsProvider> logger)
    {
        _siteService = siteService;
        _secretManager = secretManager;
        _logger = logger;
    }

    public async Task<MailchimpCredentials> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_read)
        {
            return _credentials;
        }

        var settings = await _siteService.GetSettingsAsync<MailchimpSettings>();

        if (settings.IsConfigured())
        {
            var secretName = settings.SecretName.Trim();
            var secret = await _secretManager.GetSecretAsync<TextSecret>(secretName);

            if (string.IsNullOrWhiteSpace(secret?.Text))
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("The Mailchimp settings name the secret {SecretName}, which is missing, empty or not a text secret, so Mailchimp is off.", secretName.SanitizeLogValue());
                }
            }
            else
            {
                _credentials = MailchimpCredentials.Create(secret.Text, settings.ServerPrefix);
            }
        }

        _read = true;

        return _credentials;
    }
}
