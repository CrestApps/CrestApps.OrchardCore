using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.Mvc.Web.Areas.AIChat.Models;
using CrestApps.Core.Mvc.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Services;

internal sealed class MvcCopilotOptionsConfiguration : IConfigureOptions<CopilotOptions>
{
    private const string ProtectorPurpose = "CrestApps.Core.Mvc.Web.CopilotSettings";

    private readonly AppDataSettingsService<CopilotSettings> _settingsService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<MvcCopilotOptionsConfiguration> _logger;

    public MvcCopilotOptionsConfiguration(
        AppDataSettingsService<CopilotSettings> settingsService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<MvcCopilotOptionsConfiguration> logger)
    {
        _settingsService = settingsService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public void Configure(CopilotOptions options)
    {
        var settings = _settingsService.GetAsync().GetAwaiter().GetResult();

        if (settings == null)
        {
            return;
        }

        options.AuthenticationType = settings.AuthenticationType;
        options.ClientId = settings.ClientId;
        options.Scopes = settings.Scopes ?? ["user:email", "read:org"];
        options.ProviderType = settings.ProviderType;
        options.BaseUrl = settings.BaseUrl;
        options.WireApi = settings.WireApi ?? "completions";
        options.DefaultModel = settings.DefaultModel;
        options.AzureApiVersion = settings.AzureApiVersion;

        var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);

        if (!string.IsNullOrWhiteSpace(settings.ProtectedClientSecret))
        {
            try
            {
                options.ClientSecret = protector.Unprotect(settings.ProtectedClientSecret);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unprotect Copilot client secret.");
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
                _logger.LogWarning(ex, "Failed to unprotect Copilot API key.");
            }
        }
    }
}
