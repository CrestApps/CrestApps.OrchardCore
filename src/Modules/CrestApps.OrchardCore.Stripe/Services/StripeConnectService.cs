using System.Net.Http;
using CrestApps.OrchardCore.Stripe.Drivers;
using CrestApps.OrchardCore.Stripe.Endpoints;
using CrestApps.OrchardCore.Stripe.Models;
using CrestApps.OrchardCore.Stripe.Workflows;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Verifies a Stripe secret key and automatically provisions the webhook endpoint, persisting the resolved
/// account identifier and credentials into the tenant-level Stripe settings.
/// </summary>
public sealed class StripeConnectService : IStripeConnectService
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IShellReleaseManager _shellReleaseManager;
    private readonly StripeWorkflowNotifier _workflowNotifier;
    private readonly ILogger<StripeConnectService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeConnectService"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read and persist Stripe settings.</param>
    /// <param name="dataProtectionProvider">The provider used to protect and unprotect Stripe secrets.</param>
    /// <param name="shellReleaseManager">The manager used to reload the tenant after the connection changes.</param>
    /// <param name="workflowNotifier">The notifier used to raise Stripe workflow events on failures.</param>
    /// <param name="logger">The logger used to record connection failures.</param>
    public StripeConnectService(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IShellReleaseManager shellReleaseManager,
        StripeWorkflowNotifier workflowNotifier,
        ILogger<StripeConnectService> logger)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _shellReleaseManager = shellReleaseManager;
        _workflowNotifier = workflowNotifier;
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
        var protector = _dataProtectionProvider.CreateProtector(StripeSettingsDisplayDriver.ProtectionPurpose);

        var secretPrefix = isLive ? "sk_live_" : "sk_test_";
        var publishablePrefix = isLive ? "pk_live_" : "pk_test_";

        var effectiveSecret = secretKey?.Trim();

        if (!string.IsNullOrEmpty(effectiveSecret))
        {
            if (!effectiveSecret.StartsWith(secretPrefix, StringComparison.Ordinal))
            {
                return StripeConnectionResult.Failure($"The secret key must start with '{secretPrefix}'.");
            }
        }
        else
        {
            effectiveSecret = GetStoredSecret(settings, isLive, protector);
        }

        if (string.IsNullOrEmpty(effectiveSecret))
        {
            return StripeConnectionResult.Failure("Enter your Stripe secret key before connecting.");
        }

        var effectivePublishable = publishableKey?.Trim();

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

        // Persist the verified credentials first so API calls work even if the webhook cannot be created.
        if (isLive)
        {
            settings.LivePrivateSecret = protector.Protect(effectiveSecret);
            settings.LiveAccountId = accountId;

            if (!string.IsNullOrEmpty(effectivePublishable))
            {
                settings.LivePublishableKey = effectivePublishable;
            }
        }
        else
        {
            settings.TestPrivateSecret = protector.Protect(effectiveSecret);
            settings.TestAccountId = accountId;

            if (!string.IsNullOrEmpty(effectivePublishable))
            {
                settings.TestPublishableKey = effectivePublishable;
            }
        }

        // Best-effort removal of a webhook left over from a previous connection, so reconnecting does not
        // leave an orphaned endpoint behind at Stripe.
        var existingWebhookId = isLive ? settings.LiveWebhookId : settings.TestWebhookId;

        await TryDeleteWebhookAsync(effectiveSecret, existingWebhookId);

        string webhookWarning = null;

        try
        {
            var webhookService = new WebhookEndpointService(new StripeClient(effectiveSecret));

            var webhook = await webhookService.CreateAsync(new WebhookEndpointCreateOptions
            {
                Url = webhookUrl,
                EnabledEvents = [.. CreateWebhookEndpoint.SupportedEvents],
                Description = "Auto-provisioned by CrestApps Orchard Core.",
            });

            if (isLive)
            {
                settings.LiveWebhookId = webhook.Id;
                settings.LiveWebhookSecret = string.IsNullOrEmpty(webhook.Secret) ? settings.LiveWebhookSecret : protector.Protect(webhook.Secret);
            }
            else
            {
                settings.TestWebhookId = webhook.Id;
                settings.TestWebhookSecret = string.IsNullOrEmpty(webhook.Secret) ? settings.TestWebhookSecret : protector.Protect(webhook.Secret);
            }
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Verified the Stripe account '{AccountId}' but could not provision the webhook endpoint.", accountId);

            await TriggerFailureAsync("webhook_provisioning", ex.Message);

            webhookWarning = ex.StripeError?.Message ?? ex.Message;
        }

        site.Put(settings);

        await _siteService.UpdateSiteSettingsAsync(site);

        _shellReleaseManager.RequestRelease();

        if (webhookWarning is null)
        {
            return StripeConnectionResult.Success($"Connected the Stripe account '{accountId}' and provisioned the webhook.", accountId);
        }

        return StripeConnectionResult.Success($"Connected the Stripe account '{accountId}', but the webhook could not be created automatically ({webhookWarning}). Payments will work; to receive events, make sure this site is publicly reachable and connect again, or add a webhook signing secret manually.", accountId);
    }

    /// <inheritdoc/>
    public async Task<StripeConnectionResult> DisconnectAsync(bool isLive)
    {
        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<StripeSettings>();
        var protector = _dataProtectionProvider.CreateProtector(StripeSettingsDisplayDriver.ProtectionPurpose);

        var accountId = isLive ? settings.LiveAccountId : settings.TestAccountId;
        var storedSecret = GetStoredSecret(settings, isLive, protector);
        var webhookId = isLive ? settings.LiveWebhookId : settings.TestWebhookId;

        if (string.IsNullOrEmpty(accountId) && string.IsNullOrEmpty(storedSecret))
        {
            return StripeConnectionResult.Failure("There is no connected Stripe account to disconnect.");
        }

        await TryDeleteWebhookAsync(storedSecret, webhookId);

        ClearConnection(settings, isLive);

        site.Put(settings);

        await _siteService.UpdateSiteSettingsAsync(site);

        _shellReleaseManager.RequestRelease();

        return StripeConnectionResult.Success(
            string.IsNullOrEmpty(accountId)
                ? "Disconnected the Stripe account."
                : $"Disconnected the Stripe account '{accountId}'.",
            accountId);
    }

    private static string GetStoredSecret(StripeSettings settings, bool isLive, IDataProtector protector)
    {
        var stored = isLive ? settings.LivePrivateSecret : settings.TestPrivateSecret;

        return string.IsNullOrEmpty(stored) ? null : protector.Unprotect(stored);
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
