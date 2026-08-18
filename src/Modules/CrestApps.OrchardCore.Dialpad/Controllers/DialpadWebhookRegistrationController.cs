using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Dialpad.Models;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Dialpad.Controllers;

/// <summary>
/// Provides an admin endpoint for registering Dialpad call-event webhooks.
/// </summary>
public sealed class DialpadWebhookRegistrationController : Controller
{
    private static readonly string[] _callStates =
    [
        "calling",
        "preanswer",
        "ringing",
        "connected",
        "hold",
        "hangup",
        "missed",
        "voicemail",
        "recording",
    ];

    private readonly IAuthorizationService _authorizationService;
    private readonly ITelephonyAuthenticationService _authenticationService;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IShellReleaseManager _shellReleaseManager;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialpadWebhookRegistrationController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="authenticationService">The telephony authentication service.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="shellReleaseManager">The shell release manager.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialpadWebhookRegistrationController(
        IAuthorizationService authorizationService,
        ITelephonyAuthenticationService authenticationService,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        IShellReleaseManager shellReleaseManager,
        ILogger<DialpadWebhookRegistrationController> logger,
        IStringLocalizer<DialpadWebhookRegistrationController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _authenticationService = authenticationService;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _httpClientFactory = httpClientFactory;
        _shellReleaseManager = shellReleaseManager;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Registers the active environment's Dialpad webhook and call-event subscription.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("dialpad/webhook/register", "DialpadWebhookRegister")]
    public async Task<IActionResult> Register(CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbid();
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<DialpadSettings>();

        if (!settings.IsEnabled)
        {
            return BadRequest(new
            {
                success = false,
                message = S["Enable and save the Dialpad provider before registering the webhook."].Value,
            });
        }

        var environment = settings.GetActiveEnvironmentSettings();

        if (!string.IsNullOrEmpty(environment.WebhookId) &&
            !string.IsNullOrEmpty(environment.CallEventSubscriptionId))
        {
            return Ok(new
            {
                success = true,
                message = S["Dialpad webhook registration already exists. Webhook id: {0}. Subscription id: {1}.", environment.WebhookId, environment.CallEventSubscriptionId].Value,
                webhookId = environment.WebhookId,
                subscriptionId = environment.CallEventSubscriptionId,
            });
        }

        var apiToken = await GetWebhookRegistrationBearerTokenAsync(environment, cancellationToken);

        if (string.IsNullOrEmpty(apiToken))
        {
            return BadRequest(new
            {
                success = false,
                message = S["Automatic webhook registration requires either a saved Dialpad Admin API key or a connected Dialpad admin OAuth account for the current user."].Value,
            });
        }

        var webhookUrl = BuildWebhookUrl(site);
        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var client = CreateClient(settings, environment, apiToken);

        var webhookId = await CreateWebhookAsync(client, webhookUrl, secret, cancellationToken);

        if (string.IsNullOrEmpty(webhookId))
        {
            return BadRequest(new
            {
                success = false,
                message = S["Dialpad did not create the call-event webhook. Check the saved webhook registration API key and the Dialpad environment host."].Value,
            });
        }

        if (!long.TryParse(webhookId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var endpointId))
        {
            _logger.LogError("Dialpad returned webhook id {WebhookId}, which cannot be used as a call-event subscription endpoint id.", webhookId.SanitizeLogValue());

            return BadRequest(new
            {
                success = false,
                message = S["Dialpad created the webhook but returned an invalid webhook id."].Value,
            });
        }

        var subscriptionId = await CreateCallEventSubscriptionAsync(client, endpointId, cancellationToken);

        if (string.IsNullOrEmpty(subscriptionId))
        {
            return BadRequest(new
            {
                success = false,
                message = S["Dialpad created the webhook but did not create the call-event subscription. Check the API key permissions and call-event subscription access."].Value,
            });
        }

        environment.WebhookSigningSecret = _dataProtectionProvider
            .CreateProtector(DialpadConstants.WebhookProtectorName)
            .Protect(secret);
        environment.WebhookId = webhookId;
        environment.CallEventSubscriptionId = subscriptionId;

        site.Put(settings);

        await _siteService.UpdateSiteSettingsAsync(site);

        _shellReleaseManager.RequestRelease();

        return Ok(new
        {
            success = true,
            message = S["Dialpad webhook and call-event subscription registered. Webhook id: {0}. Subscription id: {1}.", webhookId, subscriptionId].Value,
            webhookId,
            subscriptionId,
        });
    }

    private async Task<string> GetWebhookRegistrationBearerTokenAsync(
        DialpadEnvironmentSettings environment,
        CancellationToken cancellationToken)
    {
        var authenticationType = GetEffectiveWebhookRegistrationAuthenticationType(environment);

        if (authenticationType == DialpadWebhookRegistrationAuthenticationType.OAuth2)
        {
            var tokens = await _authenticationService.GetValidTokensAsync(DialpadConstants.ProviderTechnicalName, cancellationToken);

            return tokens?.AccessToken;
        }

        if (authenticationType == DialpadWebhookRegistrationAuthenticationType.ApiKey)
        {
            return GetWebhookRegistrationApiToken(environment);
        }

        return null;
    }

    private string GetWebhookRegistrationApiToken(DialpadEnvironmentSettings environment)
    {
        var protectedToken = environment.WebhookRegistrationApiToken;
        var protectorName = DialpadConstants.WebhookRegistrationProtectorName;

        if (string.IsNullOrEmpty(protectedToken))
        {
            return null;
        }

        try
        {
            return _dataProtectionProvider.CreateProtector(protectorName).Unprotect(protectedToken);
        }
        catch (CryptographicException exception)
        {
            _logger.LogError(exception, "Unable to decrypt the Dialpad webhook registration API key.");

            return null;
        }
    }

    private static DialpadWebhookRegistrationAuthenticationType GetEffectiveWebhookRegistrationAuthenticationType(DialpadEnvironmentSettings environment)
    {
        if (environment.WebhookRegistrationAuthenticationType != DialpadWebhookRegistrationAuthenticationType.NotConfigured)
        {
            return environment.WebhookRegistrationAuthenticationType;
        }

        if (environment.GetEffectiveAuthenticationType() == DialpadAuthenticationType.OAuth2)
        {
            return DialpadWebhookRegistrationAuthenticationType.OAuth2;
        }

        if (!string.IsNullOrEmpty(environment.WebhookRegistrationApiToken))
        {
            return DialpadWebhookRegistrationAuthenticationType.ApiKey;
        }

        return DialpadWebhookRegistrationAuthenticationType.NotConfigured;
    }

    private string BuildWebhookUrl(ISite site)
    {
        var baseUrl = site.BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        }

        return $"{baseUrl.TrimEnd('/')}/api/dialpad/webhook/call";
    }

    private HttpClient CreateClient(DialpadSettings settings, DialpadEnvironmentSettings environment, string apiToken)
    {
        var client = _httpClientFactory.CreateClient(DialpadConstants.ProviderTechnicalName);
        var baseUrl = environment.ApiBaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DialpadConstants.GetApiBaseUrl(settings.Environment, environment.Host);
        }
        else if (!baseUrl.EndsWith('/'))
        {
            baseUrl += '/';
        }

        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        return client;
    }

    private async Task<string> CreateWebhookAsync(
        HttpClient client,
        string webhookUrl,
        string secret,
        CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(new
        {
            hook_url = webhookUrl,
            secret,
        });

        using var response = await client.PostAsync("webhooks", content, cancellationToken);

        return await ReadDialpadIdAsync(response, "webhook", cancellationToken);
    }

    private async Task<string> CreateCallEventSubscriptionAsync(
        HttpClient client,
        long endpointId,
        CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(new
        {
            endpoint_id = endpointId,
            enabled = true,
            call_states = _callStates,
        });

        using var response = await client.PostAsync("subscriptions/call", content, cancellationToken);

        return await ReadDialpadIdAsync(response, "call-event subscription", cancellationToken);
    }

    private async Task<string> ReadDialpadIdAsync(
        HttpResponseMessage response,
        string resourceName,
        CancellationToken cancellationToken)
    {
        var payload = await SafeReadContentAsync(response, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Dialpad rejected the {ResourceName} registration request with status code {StatusCode}. Response: {Response}",
                resourceName,
                response.StatusCode,
                payload.SanitizeLogValue());

            return null;
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            _logger.LogError("Dialpad returned an empty {ResourceName} registration response.", resourceName);

            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("id", out var idElement))
            {
                if (idElement.ValueKind == JsonValueKind.String)
                {
                    return idElement.GetString();
                }

                return idElement.GetRawText();
            }
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Dialpad returned an invalid {ResourceName} registration response.", resourceName);
        }

        return null;
    }

    private static async Task<string> SafeReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return string.Empty;
        }
    }
}
