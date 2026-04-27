using CrestApps.Core.AI.Claude.Models;

namespace CrestApps.OrchardCore.AI.Chat.Claude.Settings;

/// <summary>
/// Provides extension methods for claude settings.
/// </summary>
public static class ClaudeSettingsExtensions
{
    /// <summary>
    /// Performs the is configured operation.
    /// </summary>
    /// <param name="settings">The settings.</param>
    public static bool IsConfigured(this ClaudeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.AuthenticationType switch
        {
            ClaudeAuthenticationType.ApiKey => !string.IsNullOrWhiteSpace(settings.ProtectedApiKey),
            _ => false,
        };
    }

    /// <summary>
    /// Performs the is configured operation.
    /// </summary>
    /// <param name="options">The options.</param>
    public static bool IsConfigured(this ClaudeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return !string.IsNullOrWhiteSpace(options.ApiKey);
    }
}
