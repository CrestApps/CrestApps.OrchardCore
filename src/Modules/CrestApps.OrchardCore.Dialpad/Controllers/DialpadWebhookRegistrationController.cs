using System.Security.Cryptography;
using CrestApps.OrchardCore.Dialpad.Models;
using CrestApps.OrchardCore.Dialpad.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Dialpad.Controllers;

/// <summary>
/// Provides admin endpoints for registering and disconnecting the company-level Dialpad call-event webhook.
/// The signing secret is committed to the database first and the Dialpad webhook is created from a deferred
/// task, so Dialpad can read the committed secret when it verifies the webhook.
/// </summary>
public sealed class DialpadWebhookRegistrationController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITelephonyAuthenticationService _authenticationService;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IDialpadWebhookApiService _webhookApiService;
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
    /// <param name="webhookApiService">The Dialpad webhook API service.</param>
    /// <param name="shellReleaseManager">The shell release manager.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialpadWebhookRegistrationController(
        IAuthorizationService authorizationService,
        ITelephonyAuthenticationService authenticationService,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IDialpadWebhookApiService webhookApiService,
        IShellReleaseManager shellReleaseManager,
        ILogger<DialpadWebhookRegistrationController> logger,
        IStringLocalizer<DialpadWebhookRegistrationController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _authenticationService = authenticationService;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _webhookApiService = webhookApiService;
        _shellReleaseManager = shellReleaseManager;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Starts registering the active environment's Dialpad webhook and call-event subscription. When the
    /// admin account method is selected and no valid token is available, the response asks the browser to
    /// start the Dialpad sign-in flow before continuing.
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

        if (IsWebhookRegistrationComplete(environment))
        {
            return Ok(new
            {
                success = true,
                message = S["Dialpad webhook registration already exists. Webhook id: {0}. Subscription id: {1}.", environment.WebhookId, environment.CallEventSubscriptionId].Value,
                webhookId = environment.WebhookId,
                subscriptionId = environment.CallEventSubscriptionId,
            });
        }

        var registrationAuthenticationType = GetEffectiveWebhookRegistrationAuthenticationType(environment);
        var bearerToken = await GetWebhookRegistrationBearerTokenAsync(environment, cancellationToken);

        if (string.IsNullOrEmpty(bearerToken))
        {
            if (registrationAuthenticationType == DialpadWebhookRegistrationAuthenticationType.OAuth2)
            {
                var authorizationUrl = BuildOAuthConnectUrl();

                if (!string.IsNullOrEmpty(authorizationUrl))
                {
                    return Ok(new
                    {
                        success = false,
                        requiresAuthentication = true,
                        authorizationUrl,
                        message = S["Sign in with a Dialpad company administrator account so the app can register the webhook."].Value,
                    });
                }
            }

            return BuildMissingRegistrationTokenResult(registrationAuthenticationType);
        }

        await StartRegistrationAsync(site, settings, environment, bearerToken);

        return Ok(new
        {
            success = true,
            registrationStarted = true,
            message = S["Registering the Dialpad webhook. This can take a moment; the page refreshes when it finishes."].Value,
        });
    }

    /// <summary>
    /// Completes admin-account webhook registration after the Dialpad sign-in flow returns. This runs
    /// server-side so the freshly issued access token is used to register the webhook, then redirects back
    /// to the settings page.
    /// </summary>
    /// <param name="returnUrl">The settings page URL to return to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [HttpGet]
    [Admin("dialpad/webhook/complete-registration", "DialpadWebhookCompleteRegistration")]
    public async Task<IActionResult> CompleteRegistration(string returnUrl, CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbid();
        }

        var settingsUrl = GetLocalReturnUrl(returnUrl);
        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<DialpadSettings>();

        if (!settings.IsEnabled)
        {
            return Redirect(settingsUrl);
        }

        var environment = settings.GetActiveEnvironmentSettings();

        if (IsWebhookRegistrationComplete(environment))
        {
            return Redirect(settingsUrl);
        }

        var bearerToken = await GetWebhookRegistrationBearerTokenAsync(environment, cancellationToken);

        if (string.IsNullOrEmpty(bearerToken))
        {
            return Redirect(QueryHelpers.AddQueryString(settingsUrl, "dialpadWebhookError", "1"));
        }

        await StartRegistrationAsync(site, settings, environment, bearerToken);

        return Redirect(QueryHelpers.AddQueryString(settingsUrl, "dialpadWebhookRegistering", "1"));
    }

    /// <summary>
    /// Reports whether the active environment's Dialpad webhook registration is complete. The settings page
    /// polls this endpoint while the deferred registration finishes.
    /// </summary>
    [HttpGet]
    [Admin("dialpad/webhook/status", "DialpadWebhookStatus")]
    public async Task<IActionResult> Status()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbid();
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<DialpadSettings>();
        var environment = settings.GetActiveEnvironmentSettings();

        return Ok(new
        {
            registered = IsWebhookRegistrationComplete(environment),
        });
    }

    /// <summary>
    /// Disconnects the active environment's Dialpad webhook and call-event subscription.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("dialpad/webhook/disconnect", "DialpadWebhookDisconnect")]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbid();
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<DialpadSettings>();
        var environment = settings.GetActiveEnvironmentSettings();
        var bearerToken = await GetWebhookRegistrationBearerTokenAsync(environment, cancellationToken);

        if (string.IsNullOrEmpty(bearerToken))
        {
            return BuildMissingRegistrationTokenResult(GetEffectiveWebhookRegistrationAuthenticationType(environment));
        }

        var baseUrl = ResolveApiBaseUrl(settings, environment);

        if (!await _webhookApiService.DeleteAsync(baseUrl, bearerToken, environment.WebhookId, environment.CallEventSubscriptionId, cancellationToken))
        {
            return BadRequest(new
            {
                success = false,
                message = S["Dialpad did not delete the saved webhook registration. Check the saved webhook registration credentials and try again."].Value,
            });
        }

        environment.WebhookSigningSecret = null;
        environment.WebhookId = null;
        environment.CallEventSubscriptionId = null;

        site.Put(settings);

        await _siteService.UpdateSiteSettingsAsync(site);

        _shellReleaseManager.RequestRelease();

        return Ok(new
        {
            success = true,
            message = S["Dialpad webhook registration disconnected."].Value,
        });
    }

    private async Task StartRegistrationAsync(
        ISite site,
        DialpadSettings settings,
        DialpadEnvironmentSettings environment,
        string bearerToken)
    {
        var oldWebhookId = environment.WebhookId;
        var oldCallEventSubscriptionId = environment.CallEventSubscriptionId;

        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        environment.WebhookSigningSecret = _dataProtectionProvider
            .CreateProtector(DialpadConstants.WebhookProtectorName)
            .Protect(secret);
        environment.WebhookId = null;
        environment.CallEventSubscriptionId = null;

        site.Put(settings);

        await _siteService.UpdateSiteSettingsAsync(site);

        _shellReleaseManager.RequestRelease();

        var baseUrl = ResolveApiBaseUrl(settings, environment);
        var webhookUrl = BuildWebhookUrl(site);
        var environmentType = settings.Environment;

        // The signing secret is committed when this request's session commits. Creating the Dialpad webhook
        // from a deferred task guarantees the committed secret is readable by the time Dialpad verifies the
        // webhook.
        ShellScope.AddDeferredTask(scope => RegisterWebhookAsync(
            scope,
            environmentType,
            baseUrl,
            bearerToken,
            webhookUrl,
            secret,
            oldWebhookId,
            oldCallEventSubscriptionId));
    }

    private static async Task RegisterWebhookAsync(
        ShellScope scope,
        DialpadEnvironment environmentType,
        string baseUrl,
        string bearerToken,
        string webhookUrl,
        string secret,
        string oldWebhookId,
        string oldCallEventSubscriptionId)
    {
        var services = scope.ServiceProvider;
        var apiService = services.GetRequiredService<IDialpadWebhookApiService>();
        var siteService = services.GetRequiredService<ISiteService>();
        var shellReleaseManager = services.GetRequiredService<IShellReleaseManager>();
        var logger = services.GetRequiredService<ILogger<DialpadWebhookRegistrationController>>();

        // Remove any previous registration this environment still points at, ignoring resources already gone.
        await apiService.DeleteAsync(baseUrl, bearerToken, oldWebhookId, oldCallEventSubscriptionId, CancellationToken.None);

        var result = await apiService.CreateAsync(baseUrl, bearerToken, webhookUrl, secret, CancellationToken.None);

        if (result is null)
        {
            logger.LogError("Dialpad webhook registration failed while creating the call-event webhook or subscription for the {Environment} environment.", environmentType);

            return;
        }

        var site = await siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<DialpadSettings>();
        var environment = settings.GetEnvironmentSettings(environmentType);

        environment.WebhookId = result.WebhookId;
        environment.CallEventSubscriptionId = result.CallEventSubscriptionId;

        site.Put(settings);

        await siteService.UpdateSiteSettingsAsync(site);

        shellReleaseManager.RequestRelease();
    }

    private BadRequestObjectResult BuildMissingRegistrationTokenResult(DialpadWebhookRegistrationAuthenticationType authenticationType)
    {
        if (authenticationType == DialpadWebhookRegistrationAuthenticationType.OAuth2)
        {
            return BadRequest(new
            {
                success = false,
                message = S["The Dialpad admin account sign-in did not provide a usable access token. Click Register webhook and sign in with a Dialpad company administrator account."].Value,
            });
        }

        return BadRequest(new
        {
            success = false,
            message = S["Automatic webhook registration requires either a saved Dialpad Admin API key or a connected Dialpad admin account for the current user."].Value,
        });
    }

    private string BuildOAuthConnectUrl()
    {
        var settingsReturnUrl = GetSettingsReturnUrl();
        var completeUrl = Url.RouteUrl("DialpadWebhookCompleteRegistration", new { returnUrl = settingsReturnUrl });

        if (string.IsNullOrEmpty(completeUrl))
        {
            return null;
        }

        var authorizationUrl = Url.RouteUrl(TelephonyConstants.RouteNames.OAuthConnect, new { returnUrl = completeUrl });

        if (!string.IsNullOrEmpty(authorizationUrl))
        {
            return authorizationUrl;
        }

        return $"{Request.PathBase}/Telephony/Connect?returnUrl={Uri.EscapeDataString(completeUrl)}";
    }

    private string GetSettingsReturnUrl()
    {
        var referer = Request.Headers.Referer.ToString();

        if (!string.IsNullOrEmpty(referer) &&
            Uri.TryCreate(referer, UriKind.Absolute, out var referrer) &&
            string.Equals(referrer.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase))
        {
            return referrer.PathAndQuery;
        }

        return Request.PathBase.HasValue ? Request.PathBase.Value : "/";
    }

    private string GetLocalReturnUrl(string returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        return Request.PathBase.HasValue ? Request.PathBase.Value : "/";
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

    private bool IsWebhookRegistrationComplete(DialpadEnvironmentSettings environment)
    {
        return !string.IsNullOrEmpty(environment.WebhookId) &&
            !string.IsNullOrEmpty(environment.CallEventSubscriptionId) &&
            HasReadableWebhookSigningSecret(environment);
    }

    private bool HasReadableWebhookSigningSecret(DialpadEnvironmentSettings environment)
    {
        if (string.IsNullOrEmpty(environment.WebhookSigningSecret))
        {
            return false;
        }

        try
        {
            _dataProtectionProvider
                .CreateProtector(DialpadConstants.WebhookProtectorName)
                .Unprotect(environment.WebhookSigningSecret);

            return true;
        }
        catch (CryptographicException exception)
        {
            _logger.LogError(exception, "Unable to decrypt the Dialpad webhook signing secret.");

            return false;
        }
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

    private static string ResolveApiBaseUrl(DialpadSettings settings, DialpadEnvironmentSettings environment)
    {
        var baseUrl = environment.ApiBaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return DialpadConstants.GetApiBaseUrl(settings.Environment, environment.Host);
        }

        return baseUrl.EndsWith('/') ? baseUrl : baseUrl + '/';
    }
}
