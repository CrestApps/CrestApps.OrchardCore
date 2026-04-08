using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.Mvc.Web.Areas.AIChat.Models;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Services;

public static class CopilotSettingsExtensions
{
    public static bool IsConfigured(this CopilotSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.AuthenticationType switch
        {
            CopilotAuthenticationType.GitHubOAuth =>
            !string.IsNullOrWhiteSpace(settings.ClientId) &&
                !string.IsNullOrWhiteSpace(settings.ProtectedClientSecret),
            CopilotAuthenticationType.ApiKey =>
            !string.IsNullOrWhiteSpace(settings.ProviderType) &&
                !string.IsNullOrWhiteSpace(settings.BaseUrl) &&
                    !string.IsNullOrWhiteSpace(settings.DefaultModel) &&
                        (!string.Equals(settings.ProviderType, "azure", StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrWhiteSpace(settings.AzureApiVersion) &&
                                !string.IsNullOrWhiteSpace(settings.ProtectedApiKey))),
            _ => false,
        };
    }

    public static bool IsConfigured(this CopilotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.AuthenticationType switch
        {
            CopilotAuthenticationType.GitHubOAuth =>
            !string.IsNullOrWhiteSpace(options.ClientId) &&
                !string.IsNullOrWhiteSpace(options.ClientSecret),
            CopilotAuthenticationType.ApiKey =>
            !string.IsNullOrWhiteSpace(options.ProviderType) &&
                !string.IsNullOrWhiteSpace(options.BaseUrl) &&
                    !string.IsNullOrWhiteSpace(options.DefaultModel) &&
                        (!string.Equals(options.ProviderType, "azure", StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrWhiteSpace(options.AzureApiVersion) &&
                                !string.IsNullOrWhiteSpace(options.ApiKey))),
            _ => false,
        };
    }
}
