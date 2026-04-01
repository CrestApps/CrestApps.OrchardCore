using CrestApps.AI.Copilot.Models;
using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Copilot;

/// <summary>
/// Configures <see cref="CopilotOptions"/> from OrchardCore site settings,
/// unprotecting encrypted secrets so the framework layer receives plain values.
/// </summary>
internal sealed class CopilotOptionsConfiguration : IConfigureOptions<CopilotOptions>
{
    private const string SettingsProtectorPurpose = "CrestApps.OrchardCore.AI.Chat.Copilot.Settings";

    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<CopilotOptionsConfiguration> _logger;
    public CopilotOptionsConfiguration(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<CopilotOptionsConfiguration> logger)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public void Configure(CopilotOptions options)
    {
        // ISiteService.GetSettingsAsync is async but IConfigureOptions.Configure is sync.
        // Use GetAwaiter().GetResult() as this runs once during options resolution.
        var settings = _siteService.GetSettingsAsync<CopilotSettings>()
            .GetAwaiter().GetResult();
        options.AuthenticationType = settings.AuthenticationType;
        options.ClientId = settings.ClientId;
        options.Scopes = settings.Scopes;
        options.ProviderType = settings.ProviderType;
        options.BaseUrl = settings.BaseUrl;
        options.WireApi = settings.WireApi;
        options.DefaultModel = settings.DefaultModel;
        options.AzureApiVersion = settings.AzureApiVersion;
        // Unprotect encrypted secrets.
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
