using CrestApps.Core.AI.Copilot.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Copilot;

/// <summary>
/// Configures <see cref="CopilotOptions"/> from shell configuration (appsettings / env vars)
/// first, then overlays values from OrchardCore site settings where present,
/// unprotecting encrypted secrets so the framework layer receives plain values.
/// </summary>
internal sealed class CopilotOptionsConfiguration : IConfigureOptions<CopilotOptions>
{
    private const string SettingsProtectorPurpose = "CrestApps.OrchardCore.AI.Chat.Copilot.Settings";

    private readonly IShellConfiguration _shellConfiguration;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CopilotOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="logger">The logger.</param>
    public CopilotOptionsConfiguration(
        IShellConfiguration shellConfiguration,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<CopilotOptionsConfiguration> logger)
    {
        _shellConfiguration = shellConfiguration;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    /// <summary>
    /// Configures the .
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(CopilotOptions options)
    {
        // 1. Bind from shell configuration (appsettings.json / environment variables).
        _shellConfiguration.GetSection("CrestApps:Copilot").Bind(options);

        // 2. Overlay with OrchardCore site settings (DB-stored values take precedence).
        // ISiteService.GetSettingsAsync is async but IConfigureOptions.Configure is sync.
        // Use GetAwaiter().GetResult() as this runs once during options resolution.
        var settings = _siteService.GetSettings<CopilotSettings>();

        if (settings.AuthenticationType != default)
        {
            options.AuthenticationType = settings.AuthenticationType;
        }

        if (!string.IsNullOrWhiteSpace(settings.ClientId))
        {
            options.ClientId = settings.ClientId;
        }

        if (settings.Scopes is { Length: > 0 })
        {
            options.Scopes = settings.Scopes;
        }

        if (!string.IsNullOrWhiteSpace(settings.ProviderType))
        {
            options.ProviderType = settings.ProviderType;
        }

        if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            options.BaseUrl = settings.BaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(settings.WireApi))
        {
            options.WireApi = settings.WireApi;
        }

        if (!string.IsNullOrWhiteSpace(settings.DefaultModel))
        {
            options.DefaultModel = settings.DefaultModel;
        }

        if (!string.IsNullOrWhiteSpace(settings.AzureApiVersion))
        {
            options.AzureApiVersion = settings.AzureApiVersion;
        }

        // Unprotect encrypted secrets from site settings.
        var protector = _dataProtectionProvider.CreateProtector(SettingsProtectorPurpose);

        if (!string.IsNullOrWhiteSpace(settings.ProtectedClientSecret))
        {
            try
            {
                options.ClientSecret = protector.Unprotect(settings.ProtectedClientSecret);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unprotect Copilot client secret.");
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.ProtectedApiKey))
        {
            try
            {
                options.ApiKey = protector.Unprotect(settings.ProtectedApiKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unprotect Copilot API key.");
            }
        }
    }
}
