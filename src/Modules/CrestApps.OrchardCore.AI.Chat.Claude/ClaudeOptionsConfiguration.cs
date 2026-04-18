using CrestApps.Core.AI.Claude.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Claude;

internal sealed class ClaudeOptionsConfiguration : IConfigureOptions<ClaudeOptions>
{
    private const string SettingsProtectorPurpose = "CrestApps.OrchardCore.AI.Chat.Claude.Settings";

    private readonly IShellConfiguration _shellConfiguration;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    public ClaudeOptionsConfiguration(
        IShellConfiguration shellConfiguration,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<ClaudeOptionsConfiguration> logger)
    {
        _shellConfiguration = shellConfiguration;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public void Configure(ClaudeOptions options)
    {
        _shellConfiguration.GetSection("CrestApps:Claude").Bind(options);

        var settings = _siteService.GetSettingsAsync<ClaudeSettings>()
            .GetAwaiter().GetResult();

        if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            options.BaseUrl = settings.BaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(settings.DefaultModel))
        {
            options.DefaultModel = settings.DefaultModel;
        }

        if (settings.AuthenticationType != ClaudeAuthenticationType.ApiKey ||
            string.IsNullOrWhiteSpace(settings.ProtectedApiKey))
        {
            return;
        }

        var protector = _dataProtectionProvider.CreateProtector(SettingsProtectorPurpose);

        try
        {
            options.ApiKey = protector.Unprotect(settings.ProtectedApiKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unprotect Claude API key.");
        }
    }
}
