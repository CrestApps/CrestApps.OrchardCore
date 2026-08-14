using CrestApps.Core.AI.Copilot.Models;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Settings;

/// <summary>
/// Helper methods for validating Copilot site settings.
/// </summary>
public static class CopilotSettingsExtensions
{
    /// <summary>
    /// Determines whether the tenant has stored Copilot settings that should override shell configuration.
    /// </summary>
    /// <param name="settings">The Copilot settings.</param>
    public static bool HasStoredConfiguration(this CopilotSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return !string.IsNullOrWhiteSpace(settings.ClientId) ||
            !string.IsNullOrWhiteSpace(settings.ProtectedClientSecret) ||
            !string.IsNullOrWhiteSpace(settings.ProviderType) ||
            !string.IsNullOrWhiteSpace(settings.BaseUrl) ||
            !string.IsNullOrWhiteSpace(settings.ProtectedApiKey) ||
            !string.IsNullOrWhiteSpace(settings.DefaultModel) ||
            !string.IsNullOrWhiteSpace(settings.AzureApiVersion);
    }

    /// <summary>
    /// Determines whether the configured Copilot settings are complete enough for runtime use.
    /// </summary>
    /// <param name="settings">The Copilot settings.</param>
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
}
