namespace CrestApps.Core.AI.A2A.Models;

/// <summary>
/// Defines the authentication types supported by the A2A host.
/// </summary>
public enum A2AHostAuthenticationType
{
    /// <summary>
    /// Uses OpenID Connect authentication via the "Api" scheme.
    /// This is the default and most secure option for production environments.
    /// </summary>
    OpenId,

    /// <summary>
    /// Uses a predefined API key for authentication.
    /// The API key must be configured in the settings and provided via the Authorization header.
    /// </summary>
    ApiKey,

    /// <summary>
    /// Disables authentication completely.
    /// WARNING: This option should only be used for local development and testing.
    /// Never use this option in production environments.
    /// </summary>
    None,
}
