namespace CrestApps.OrchardCore.Dialpad.Models;

/// <summary>
/// Defines the authentication modes supported by the Dialpad provider.
/// </summary>
public enum DialpadAuthenticationType
{
    /// <summary>
    /// No authentication type has been selected yet.
    /// </summary>
    NotConfigured = 0,

    /// <summary>
    /// Use a shared Dialpad API key that belongs to one Dialpad account.
    /// </summary>
    ApiKey = 1,

    /// <summary>
    /// Authenticate each user through Dialpad OAuth 2.0.
    /// </summary>
    OAuth2 = 2,
}
