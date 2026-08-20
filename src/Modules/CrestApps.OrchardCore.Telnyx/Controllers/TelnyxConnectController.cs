using System.Security.Cryptography;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telnyx.Controllers;

/// <summary>
/// Provides admin endpoints for the one-click "Connect Telnyx" auto-provisioning flow. The Telnyx API key
/// carries full account access, so it is the only credential needed: the app uses it to find-or-create the
/// Call Control application, the Credential SIP connection, and an outbound voice profile, and to discover a
/// caller-id number.
/// </summary>
public sealed class TelnyxConnectController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ITelnyxProvisioningApiService _provisioningApiService;
    private readonly IShellReleaseManager _shellReleaseManager;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxConnectController"/> class.
    /// </summary>
    public TelnyxConnectController(
        IAuthorizationService authorizationService,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ITelnyxProvisioningApiService provisioningApiService,
        IShellReleaseManager shellReleaseManager,
        ShellSettings shellSettings,
        ILogger<TelnyxConnectController> logger,
        IStringLocalizer<TelnyxConnectController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _provisioningApiService = provisioningApiService;
        _shellReleaseManager = shellReleaseManager;
        _shellSettings = shellSettings;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Auto-provisions the Telnyx resources using the saved API key and writes the resolved ids into settings.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("telnyx/connect", "TelnyxConnect")]
    public async Task<IActionResult> Connect(CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbid();
        }

        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<TelnyxSettings>();

        if (!settings.IsEnabled)
        {
            return BadRequest(new { success = false, message = S["Enable and save the Telnyx provider with an API key before connecting."].Value });
        }

        var apiKey = Unprotect(settings.ApiKey);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return BadRequest(new { success = false, message = S["Enter and save a Telnyx API key before connecting."].Value });
        }

        var apiBaseUrl = ResolveApiBaseUrl(settings);
        var webhookUrl = BuildWebhookUrl(site);
        var resourceName = BuildResourceName();

        var result = await _provisioningApiService.ConnectAsync(apiKey, apiBaseUrl, webhookUrl, resourceName, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { success = false, message = result.Error ?? S["Telnyx connect failed."].Value });
        }

        settings.ConnectionId = result.ConnectionId;
        settings.SipConnectionId = result.SipConnectionId;

        if (!string.IsNullOrWhiteSpace(result.OutboundVoiceProfileId))
        {
            settings.OutboundVoiceProfileId = result.OutboundVoiceProfileId;
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultOutboundCallerId) && !string.IsNullOrWhiteSpace(result.SuggestedCallerId))
        {
            settings.DefaultOutboundCallerId = result.SuggestedCallerId;
        }

        site.Put(settings);
        await _siteService.UpdateSiteSettingsAsync(site);
        _shellReleaseManager.RequestRelease();

        return Ok(new
        {
            success = true,
            message = string.IsNullOrWhiteSpace(result.Warning)
                ? S["Connected to Telnyx. The connection ids were configured automatically."].Value
                : result.Warning,
            callerId = settings.DefaultOutboundCallerId,
            availableNumbers = result.AvailableNumbers,
        });
    }

    /// <summary>
    /// Reports whether the tenant is connected to Telnyx (both connection ids present).
    /// </summary>
    [HttpGet]
    [Admin("telnyx/connect/status", "TelnyxConnectStatus")]
    public async Task<IActionResult> Status()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbid();
        }

        var settings = (await _siteService.GetSiteSettingsAsync()).GetOrCreate<TelnyxSettings>();

        return Ok(new
        {
            connected = !string.IsNullOrWhiteSpace(settings.ConnectionId) && !string.IsNullOrWhiteSpace(settings.SipConnectionId),
        });
    }

    /// <summary>
    /// Deletes the provisioned Telnyx resources and clears the saved connection ids.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("telnyx/connect/disconnect", "TelnyxConnectDisconnect")]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.ManageTelephonySettings))
        {
            return Forbid();
        }

        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<TelnyxSettings>();
        var apiKey = Unprotect(settings.ApiKey);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            await _provisioningApiService.DisconnectAsync(
                apiKey,
                ResolveApiBaseUrl(settings),
                settings.ConnectionId,
                settings.SipConnectionId,
                settings.OutboundVoiceProfileId,
                cancellationToken);
        }

        settings.ConnectionId = null;
        settings.SipConnectionId = null;
        settings.OutboundVoiceProfileId = null;

        site.Put(settings);
        await _siteService.UpdateSiteSettingsAsync(site);
        _shellReleaseManager.RequestRelease();

        return Ok(new { success = true, message = S["Disconnected from Telnyx. The provisioned resources were removed."].Value });
    }

    private string Unprotect(string protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue))
        {
            return null;
        }

        try
        {
            return _dataProtectionProvider.CreateProtector(TelnyxConstants.ProtectorName).Unprotect(protectedValue);
        }
        catch (CryptographicException exception)
        {
            _logger.LogError(exception, "Unable to decrypt the Telnyx API key.");

            return null;
        }
    }

    private static string ResolveApiBaseUrl(TelnyxSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
        {
            return TelnyxConstants.DefaultApiBaseUrl;
        }

        return settings.ApiBaseUrl.EndsWith('/') ? settings.ApiBaseUrl : settings.ApiBaseUrl + '/';
    }

    private string BuildResourceName()
    {
        var tenant = string.IsNullOrWhiteSpace(_shellSettings.Name) ? "Default" : _shellSettings.Name;

        return $"CrestApps Telephony ({tenant})";
    }

    private string BuildWebhookUrl(ISite site)
    {
        var baseUrl = site.BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        }

        return $"{baseUrl.TrimEnd('/')}/{TelnyxConstants.WebhookPath}";
    }
}
