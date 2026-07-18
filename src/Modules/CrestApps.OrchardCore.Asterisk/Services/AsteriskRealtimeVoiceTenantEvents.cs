using CrestApps.OrchardCore.Asterisk.Models;
using Microsoft.AspNetCore.DataProtection;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskRealtimeVoiceTenantEvents : ModularTenantEvents
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly DefaultAsteriskOptions _defaultOptions;
    private readonly ShellSettings _shellSettings;
    private readonly AsteriskRealtimeVoiceListener _listener;
    private readonly IAsteriskAriApplicationGate _applicationGate;
    private readonly ILogger<AsteriskRealtimeVoiceTenantEvents> _logger;

    public AsteriskRealtimeVoiceTenantEvents(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<DefaultAsteriskOptions> defaultOptions,
        ShellSettings shellSettings,
        AsteriskRealtimeVoiceListener listener,
        IAsteriskAriApplicationGate applicationGate,
        ILogger<AsteriskRealtimeVoiceTenantEvents> logger)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _defaultOptions = defaultOptions.Value;
        _shellSettings = shellSettings;
        _listener = listener;
        _applicationGate = applicationGate;
        _logger = logger;
    }

    public override Task ActivatingAsync()
    {
        var listeners = ResolveListeners();

        if (listeners.Count == 0)
        {
            return Task.CompletedTask;
        }

        _listener.StartAsync(listeners);

        return Task.CompletedTask;
    }

    public override async Task TerminatingAsync()
    {
        try
        {
            await _listener.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while stopping the Asterisk real-time voice listener.");
        }

        _applicationGate.ReleaseGeneration();
    }

    private List<AsteriskResolvedSettings> ResolveListeners()
    {
        var tenantSettings = _siteService.GetSettings<AsteriskSettings>();

        // Mirror AsteriskAriClient.ResolveSettings: exactly one active Asterisk endpoint per tenant
        // (tenant-if-enabled, otherwise the host default). Every ARI operation runs through that single
        // resolved endpoint, so starting a single matching listener keeps the real-time event source
        // consistent with the client and avoids a second listener reconciling channels against a
        // different endpoint. The Contact Center-facing provider name is always the voice provider
        // technical name so inbound interactions and bindings resolve through the voice provider resolver.
        AsteriskResolvedSettings resolved;

        if (tenantSettings?.IsEnabled == true)
        {
            var protectedPassword = tenantSettings.Password;
            var password = string.IsNullOrWhiteSpace(protectedPassword)
                ? null
                : TryUnprotectPassword(protectedPassword);

            resolved = new AsteriskResolvedSettings
            {
                IsEnabled = true,
                ProviderName = AsteriskConstants.ProviderTechnicalName,
                BaseUrl = tenantSettings.BaseUrl,
                UserName = tenantSettings.UserName,
                Password = password,
                ApplicationName = tenantSettings.ApplicationName,
                EndpointTemplate = tenantSettings.EndpointTemplate,
                OutboundCallerId = tenantSettings.OutboundCallerId,
                TimeoutSeconds = tenantSettings.TimeoutSeconds,
                VoicemailContext = tenantSettings.VoicemailContext,
                VoicemailExtensionTemplate = tenantSettings.VoicemailExtensionTemplate,
                VoicemailPriority = tenantSettings.VoicemailPriority,
            };
        }
        else if (_defaultOptions.IsEnabled && _shellSettings.IsDefaultShell())
        {
            // The host-level default Asterisk connection is a single shared server credential, so only the default
            // shell may start a listener against it. A non-default tenant sharing the same ARI application would
            // cross-deliver Stasis events between tenants, so non-default tenants must configure their own Asterisk
            // settings (with a unique application name) or no listener starts.
            resolved = new AsteriskResolvedSettings
            {
                IsEnabled = true,
                ProviderName = AsteriskConstants.ProviderTechnicalName,
                BaseUrl = _defaultOptions.BaseUrl,
                UserName = _defaultOptions.UserName,
                Password = _defaultOptions.Password,
                ApplicationName = _defaultOptions.ApplicationName,
                EndpointTemplate = _defaultOptions.EndpointTemplate,
                OutboundCallerId = _defaultOptions.OutboundCallerId,
                TimeoutSeconds = _defaultOptions.TimeoutSeconds,
                VoicemailContext = _defaultOptions.VoicemailContext,
                VoicemailExtensionTemplate = _defaultOptions.VoicemailExtensionTemplate,
                VoicemailPriority = _defaultOptions.VoicemailPriority,
            };
        }
        else
        {
            return [];
        }

        AsteriskSettingsUtilities.ApplyDefaults(resolved);

        if (!AsteriskSettingsUtilities.HasRequiredConfiguration(resolved))
        {
            return [];
        }

        // The gate centralizes single-tenant ownership of the ARI application. It rejects an application that collides
        // with the host default connection (on a non-default shell) or is already owned by another tenant on this node,
        // and otherwise claims it under this shell generation's token. The ARI application name is the tenant's routing
        // identity on the shared PBX (the name the operator wires into the Asterisk dialplan as Stasis(app)), so it
        // cannot be silently rewritten to a derived per-tenant name without breaking inbound routing. Fail closed
        // instead: the tenant must configure a unique application name.
        if (!_applicationGate.TryAcquire(resolved))
        {
            _logger.LogError(
                "The Asterisk real-time voice listener for tenant '{TenantName}' was not started because its ARI application '{ApplicationName}' on server '{BaseUrl}' collides with the host default connection or is already owned by another tenant on this node. Configure a unique application name for this tenant to prevent cross-tenant Stasis event delivery.",
                _shellSettings.Name,
                resolved.ApplicationName,
                resolved.BaseUrl);

            return [];
        }

        return [resolved];
    }

    private string TryUnprotectPassword(string protectedPassword)
    {
        try
        {
            return _dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName).Unprotect(protectedPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "Failed to unprotect the tenant-configured Asterisk password for the real-time listener.");

            return null;
        }
    }
}
