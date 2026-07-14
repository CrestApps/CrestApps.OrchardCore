using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// A telephony provider that controls calls through a tenant-configured Asterisk ARI endpoint.
/// </summary>
internal sealed class AsteriskTelephonyProvider : AsteriskTelephonyProviderBase
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskTelephonyProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read the tenant-configured Asterisk settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to unprotect the stored password.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AsteriskTelephonyProvider(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        IClock clock,
        ILogger<AsteriskTelephonyProvider> logger,
        IStringLocalizer<AsteriskTelephonyProvider> stringLocalizer)
        : base(httpClientFactory, clock, logger, stringLocalizer)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override LocalizedString Name => S["Asterisk"];

    /// <inheritdoc/>
    public override TelephonyCapabilities Capabilities
        => GetCapabilities(
            _siteService.GetSettings<AsteriskSettings>()?.EndpointTemplate,
            AsteriskSettingsUtilities.HasVoicemailConfiguration(_siteService.GetSettings<AsteriskSettings>()));

    protected override string ProviderName
        => AsteriskConstants.ProviderTechnicalName;

    protected override ValueTask<AsteriskResolvedSettings> GetResolvedSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = _siteService.GetSettings<AsteriskSettings>();
        var resolved = new AsteriskResolvedSettings
        {
            IsEnabled = settings.IsEnabled,
            ProviderName = ProviderName,
            BaseUrl = settings.BaseUrl,
            UserName = settings.UserName,
            Password = UnprotectPassword(settings.Password),
            ApplicationName = settings.ApplicationName,
            EndpointTemplate = settings.EndpointTemplate,
            OutboundCallerId = settings.OutboundCallerId,
            TimeoutSeconds = settings.TimeoutSeconds,
            VoicemailContext = settings.VoicemailContext,
            VoicemailExtensionTemplate = settings.VoicemailExtensionTemplate,
            VoicemailPriority = settings.VoicemailPriority,
        };

        AsteriskSettingsUtilities.ApplyDefaults(new AsteriskConnectionSettingsAdapter(resolved));

        return ValueTask.FromResult(resolved);
    }

    private string UnprotectPassword(string protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
        {
            return null;
        }

        try
        {
            return _dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName).Unprotect(protectedPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "Failed to unprotect the tenant-configured Asterisk password.");

            return null;
        }
    }

    private sealed class AsteriskConnectionSettingsAdapter : AsteriskConnectionSettings
    {
        private readonly AsteriskResolvedSettings _settings;

        public AsteriskConnectionSettingsAdapter(AsteriskResolvedSettings settings)
        {
            _settings = settings;
        }

        public override string BaseUrl
        {
            get => _settings.BaseUrl;
            set => _settings.BaseUrl = value;
        }

        public override string UserName
        {
            get => _settings.UserName;
            set => _settings.UserName = value;
        }

        public override string ApplicationName
        {
            get => _settings.ApplicationName;
            set => _settings.ApplicationName = value;
        }

        public override string EndpointTemplate
        {
            get => _settings.EndpointTemplate;
            set => _settings.EndpointTemplate = value;
        }

        public override string OutboundCallerId
        {
            get => _settings.OutboundCallerId;
            set => _settings.OutboundCallerId = value;
        }

        public override int TimeoutSeconds
        {
            get => _settings.TimeoutSeconds;
            set => _settings.TimeoutSeconds = value;
        }

        public override string VoicemailContext
        {
            get => _settings.VoicemailContext;
            set => _settings.VoicemailContext = value;
        }

        public override string VoicemailExtensionTemplate
        {
            get => _settings.VoicemailExtensionTemplate;
            set => _settings.VoicemailExtensionTemplate = value;
        }

        public override int VoicemailPriority
        {
            get => _settings.VoicemailPriority;
            set => _settings.VoicemailPriority = value;
        }
    }
}
