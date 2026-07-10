using CrestApps.OrchardCore.Asterisk.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskRealtimeVoiceTenantEvents : ModularTenantEvents
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly DefaultAsteriskOptions _defaultOptions;
    private readonly AsteriskRealtimeVoiceListener _listener;
    private readonly ILogger<AsteriskRealtimeVoiceTenantEvents> _logger;

    public AsteriskRealtimeVoiceTenantEvents(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<DefaultAsteriskOptions> defaultOptions,
        AsteriskRealtimeVoiceListener listener,
        ILogger<AsteriskRealtimeVoiceTenantEvents> logger)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _defaultOptions = defaultOptions.Value;
        _listener = listener;
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
            _logger.LogError(ex, "An error occurred while stopping the Asterisk real-time voice listener.");
        }
    }

    private List<AsteriskResolvedSettings> ResolveListeners()
    {
        var listeners = new List<AsteriskResolvedSettings>();
        var tenantSettings = _siteService.GetSettings<AsteriskSettings>();
        var protectedPassword = tenantSettings.Password;
        var password = string.IsNullOrWhiteSpace(protectedPassword)
            ? null
            : TryUnprotectPassword(protectedPassword);

        var tenantResolvedSettings = new AsteriskResolvedSettings
        {
            IsEnabled = tenantSettings.IsEnabled,
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

        AsteriskSettingsUtilities.ApplyDefaults(tenantResolvedSettings);

        if (AsteriskSettingsUtilities.HasRequiredConfiguration(tenantResolvedSettings))
        {
            listeners.Add(tenantResolvedSettings);
        }

        if (_defaultOptions.IsEnabled)
        {
            var defaultResolvedSettings = new AsteriskResolvedSettings
            {
                IsEnabled = _defaultOptions.IsEnabled,
                ProviderName = AsteriskConstants.DefaultProviderTechnicalName,
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

            AsteriskSettingsUtilities.ApplyDefaults(defaultResolvedSettings);

            if (AsteriskSettingsUtilities.HasRequiredConfiguration(defaultResolvedSettings))
            {
                listeners.Add(defaultResolvedSettings);
            }
        }

        return listeners;
    }

    private string TryUnprotectPassword(string protectedPassword)
    {
        try
        {
            return _dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName).Unprotect(protectedPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect the tenant-configured Asterisk password for the real-time listener.");

            return null;
        }
    }
}
