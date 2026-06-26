namespace CrestApps.OrchardCore.DialPad.Models;

/// <summary>
/// Defines the authentication modes supported by the DialPad provider.
/// </summary>
public enum DialPadAuthenticationType
{
    /// <summary>
    /// No authentication type has been selected yet.
    /// </summary>
    NotConfigured = 0,

    /// <summary>
    /// Use a shared DialPad API key that belongs to one DialPad account.
    /// </summary>
    ApiKey = 1,

    /// <summary>
    /// Authenticate each user through DialPad OAuth 2.0.
    /// </summary>
    OAuth2 = 2,
}
