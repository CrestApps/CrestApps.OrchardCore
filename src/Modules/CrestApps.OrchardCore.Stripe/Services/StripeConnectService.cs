using System.Net.Http;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Drivers;
using CrestApps.OrchardCore.Stripe.Endpoints;
using CrestApps.OrchardCore.Stripe.Models;
using CrestApps.OrchardCore.Stripe.Workflows;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.Environment.Options;
using OrchardCore.Settings;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Verifies a Stripe secret key and automatically provisions the webhook endpoint, persisting the resolved
/// account identifier and credentials into the tenant-level Stripe settings.
/// </summary>
/// <remarks>
/// A key supplied by the application configuration (the <c>CrestApps_Stripe</c> section) is used as it is and never
/// copied into the stored settings, so a secret kept in a vault stays out of the database. When the configuration
/// supplies the webhook signing secret, no webhook is created, because a new endpoint would come with a signing secret
/// the configured one would shadow and every event would then fail verification.
/// </remarks>
public sealed class StripeConnectService : IStripeConnectService
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;
    private readonly StripeWorkflowNotifier _workflowNotifier;
    private readonly StripeConfigurationOptions _configurationOptions;
    private readonly ILogger<StripeConnectService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeConnectService"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read and persist Stripe settings.</param>
    /// <param name="dataProtectionProvider">The provider used to protect and unprotect Stripe secrets.</param>
    /// <param name="optionsUpdateNotifier">The notifier that refreshes the runtime Stripe options after the connection changes.</param>
    /// <param name="workflowNotifier">The notifier used to raise Stripe workflow events on failures.</param>
    /// <param name="configurationOptions">The Stripe values supplied by the application configuration.</param>
    /// <param name="logger">The logger used to record connection failures.</param>
    public StripeConnectService(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IOptionsUpdateNotifier optionsUpdateNotifier,
        StripeWorkflowNotifier workflowNotifier,
        IOptions<StripeConfigurationOptions> configurationOptions,
        ILogger<StripeConnectService> logger)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _optionsUpdateNotifier = optionsUpdateNotifier;
        _workflowNotifier = workflowNotifier;
        _configurationOptions = configurationOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsConnectedAsync(bool isLive)
    {
        var settings = await _siteService.GetSettingsAsync<StripeSettings>();

        return !string.IsNullOrEmpty(isLive ? settings.LiveAccountId : settings.TestAccountId);
    }

    /// <inheritdoc/>
    public async Task<StripeConnectionResult> ConnectAsync(bool isLive, string publishableKey, string secretKey, string webhookUrl)
    {
        ArgumentException.ThrowIfNullOrEmpty(webhookUrl);

        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<StripeSettings>();

        var secretPrefix = isLive ? "sk_live_" : "sk_test_";
        var publishablePrefix = isLive ? "pk_live_" : "pk_test_";
        var configured = _configurationOptions.GetKeys(isLive);

        var effectiveSecret = secretKey?.Trim();

        if (configured.HasSecretKey)
        {
            // The configured key wins at runtime, so it is the one to verify, whatever the form carried.
            effectiveSecret = configured.SecretKey.Trim();
        }
        else if (!string.IsNullOrEmpty(effectiveSecret))
        {
            if (!effectiveSecret.StartsWith(secretPrefix, StringComparison.Ordinal))
            {
                return StripeConnectionResult.Failure($"The secret key must start with '{secretPrefix}'.");
            }
        }
        else
        {
            effectiveSecret = GetStoredSecret(settings, isLive);
        }

        if (string.IsNullOrEmpty(effectiveSecret))
        {
            return StripeConnectionResult.Failure("Enter your Stripe secret key before connecting.");
        }

        var effectivePublishable = configured.HasPublishableKey
            ? null
            : publishableKey?.Trim();

        if (!string.IsNullOrEmpty(effectivePublishable) && !effectivePublishable.StartsWith(publishablePrefix, StringComparison.Ordinal))
        {
            return StripeConnectionResult.Failure($"The publishable key must start with '{publishablePrefix}'.");
        }

        string accountId;

        try
        {
            var client = new StripeClient(effectiveSecret);
            var account = await client.RequestAsync<Account>(HttpMethod.Get, "/v1/account", new AccountGetOptions(), requestOptions: null);

            accountId = account.Id;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to verify the Stripe secret key while connecting.");

            await TriggerFailureAsync("verify_key", ex.Message);

            return StripeConnectionResult.Failure($"Stripe rejected the secret key: {ex.StripeError?.Message ?? ex.Message}");
        }

        // Persist the verified credentials first so API calls work even if the webhook cannot be created. A secret
        // that comes from the configuration is never copied into the stored settings.
        var storedSecret = configured.HasSecretKey
            ? null
            : _dataProtectionProvider.ProtectSetting(StripeSettingsDisplayDriver.ProtectionPurpose, effectiveSecret);

        if (isLive)
        {
            settings.LivePrivateSecret = storedSecret ?? settings.LivePrivateSecret;
            settings.LiveAccountId = accountId;

            if (!string.IsNullOrEmpty(effectivePublishable))
            {
                settings.LivePublishableKey = effectivePublishable;
            }
        }
        else
        {
            settings.TestPrivateSecret = storedSecret ?? settings.TestPrivateSecret;
            settings.TestAccountId = accountId;

            if (!string.IsNullOrEmpty(effectivePublishable))
            {
                settings.TestPublishableKey = effectivePublishable;
            }
        }

        string webhookWarning = null;
        string webhookNote = null;

        if (configured.HasWebhookSecret)
        {
            webhookNote = " The webhook signing secret is configured from the application settings, so no webhook was created; register the webhook in the Stripe dashboard with that signing secret.";
        }
        else
        {
            webhookWarning = await ProvisionWebhookAsync(settings, isLive, effectiveSecret, webhookUrl, accountId);
        }

        var walletDomain = await TryRegisterWalletDomainAsync(effectiveSecret, webhookUrl);

        site.Put(settings);

        await _siteService.UpdateSiteSettingsAsync(site);

        _optionsUpdateNotifier.RequestUpdate<StripeOptions>();

        var walletNote = walletDomain is null
            ? string.Empty
            : $" Apple Pay and Google Pay are enabled for '{walletDomain}'.";

        if (webhookWarning is not null)
        {
            return StripeConnectionResult.Success($"Connected the Stripe account '{accountId}', but the webhook could not be created automatically ({webhookWarning}). Payments will work; to receive events, make sure this site is publicly reachable and connect again, or add a webhook signing secret manually.{walletNote}", accountId);
        }

        if (webhookNote is not null)
        {
            return StripeConnectionResult.Success($"Connected the Stripe account '{accountId}'.{webhookNote}{walletNote}", accountId);
        }

        return StripeConnectionResult.Success($"Connected the Stripe account '{accountId}' and provisioned the webhook.{walletNote}", accountId);
    }

    private async Task<string> ProvisionWebhookAsync(
        StripeSettings settings,
        bool isLive,
        string secretKey,
        string webhookUrl,
        string accountId)
    {
        // Best-effort removal of a webhook left over from a previous connection, so reconnecting does not
        // leave an orphaned endpoint behind at Stripe.
        var existingWebhookId = isLive ? settings.LiveWebhookId : settings.TestWebhookId;

        await TryDeleteWebhookAsync(secretKey, existingWebhookId);

        try
        {
            var webhookService = new WebhookEndpointService(new StripeClient(secretKey));

            var webhook = await webhookService.CreateAsync(new WebhookEndpointCreateOptions
            {
                Url = webhookUrl,
                EnabledEvents = [.. CreateWebhookEndpoint.SupportedEvents],
                Description = "Auto-provisioned by CrestApps Orchard Core.",
            });

            if (isLive)
            {
                settings.LiveWebhookId = webhook.Id;
                settings.LiveWebhookSecret = string.IsNullOrEmpty(webhook.Secret) ? settings.LiveWebhookSecret : _dataProtectionProvider.ProtectSetting(StripeSettingsDisplayDriver.ProtectionPurpose, webhook.Secret);
            }
            else
            {
                settings.TestWebhookId = webhook.Id;
                settings.TestWebhookSecret = string.IsNullOrEmpty(webhook.Secret) ? settings.TestWebhookSecret : _dataProtectionProvider.ProtectSetting(StripeSettingsDisplayDriver.ProtectionPurpose, webhook.Secret);
            }

            return null;
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Verified the Stripe account '{AccountId}' but could not provision the webhook endpoint.", accountId);

            await TriggerFailureAsync("webhook_provisioning", ex.Message);

            return ex.StripeError?.Message ?? ex.Message;
        }
    }

    /// <inheritdoc/>
    public async Task<StripeConnectionResult> DisconnectAsync(bool isLive)
    {
        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<StripeSettings>();

        var configured = _configurationOptions.GetKeys(isLive);

        var accountId = isLive ? settings.LiveAccountId : settings.TestAccountId;
        var storedSecret = GetStoredSecret(settings, isLive);
        var webhookId = isLive ? settings.LiveWebhookId : settings.TestWebhookId;

        if (string.IsNullOrEmpty(accountId) && string.IsNullOrEmpty(storedSecret))
        {
            return StripeConnectionResult.Failure("There is no connected Stripe account to disconnect.");
        }

        // The webhook was created with the key in effect, which is the configured one when the configuration has it.
        var effectiveSecret = configured.HasSecretKey
            ? configured.SecretKey.Trim()
            : storedSecret;

        await TryDeleteWebhookAsync(effectiveSecret, webhookId);

        ClearConnection(settings, isLive);

        site.Put(settings);

        await _siteService.UpdateSiteSettingsAsync(site);

        _optionsUpdateNotifier.RequestUpdate<StripeOptions>();

        var message = string.IsNullOrEmpty(accountId)
            ? "Disconnected the Stripe account."
            : $"Disconnected the Stripe account '{accountId}'.";

        if (configured.HasAny)
        {
            message += " Keys configured from the application settings are not removed and stay in use; remove them from the application settings to stop using them.";
        }

        return StripeConnectionResult.Success(message, accountId);
    }

    private string GetStoredSecret(StripeSettings settings, bool isLive)
        => _dataProtectionProvider.UnprotectSetting(StripeSettingsDisplayDriver.ProtectionPurpose, isLive ? settings.LivePrivateSecret : settings.TestPrivateSecret);

    // Wallet buttons (Apple Pay above all) are only offered on a domain registered with the Stripe account, so the
    // site's own domain is registered while connecting. A site that is not publicly reachable cannot be
    // registered, and failing to register never fails the connection: card payments work either way.
    private async Task<string> TryRegisterWalletDomainAsync(string secretKey, string siteUrl)
    {
        if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var uri) ||
            uri.IsLoopback ||
            uri.HostNameType != UriHostNameType.Dns ||
            !uri.Host.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var domainService = new PaymentMethodDomainService(new StripeClient(secretKey));
            var existing = await domainService.ListAsync(new PaymentMethodDomainListOptions { DomainName = uri.Host });
            var domain = existing.Data.FirstOrDefault();

            if (domain is null)
            {
                await domainService.CreateAsync(new PaymentMethodDomainCreateOptions { DomainName = uri.Host, Enabled = true });
            }
            else if (!domain.Enabled)
            {
                await domainService.UpdateAsync(domain.Id, new PaymentMethodDomainUpdateOptions { Enabled = true });
            }

            return uri.Host;
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Could not register the domain '{Domain}' for wallet payments with Stripe.", uri.Host);

            return null;
        }
    }

    private async Task TryDeleteWebhookAsync(string secretKey, string webhookId)
    {
        if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(webhookId))
        {
            return;
        }

        try
        {
            var webhookService = new WebhookEndpointService(new StripeClient(secretKey));

            await webhookService.DeleteAsync(webhookId);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Failed to delete the Stripe webhook '{WebhookId}'.", webhookId);
        }
    }

    private static void ClearConnection(StripeSettings settings, bool isLive)
    {
        if (isLive)
        {
            settings.LivePublishableKey = null;
            settings.LivePrivateSecret = null;
            settings.LiveWebhookSecret = null;
            settings.LiveAccountId = null;
            settings.LiveWebhookId = null;

            return;
        }

        settings.TestPublishableKey = null;
        settings.TestPrivateSecret = null;
        settings.TestWebhookSecret = null;
        settings.TestAccountId = null;
        settings.TestWebhookId = null;
    }

    private Task TriggerFailureAsync(string operation, string message)
    {
        return _workflowNotifier.TriggerAsync(
            StripeWorkflowEventNames.RequestFailed,
            new Dictionary<string, object>
            {
                { "Operation", operation },
                { "Message", message },
            },
            correlationId: $"StripeConnect_{operation}");
    }
}
