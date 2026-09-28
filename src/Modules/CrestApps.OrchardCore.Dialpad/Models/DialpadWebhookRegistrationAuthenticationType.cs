namespace CrestApps.OrchardCore.Dialpad.Models;

/// <summary>
/// Defines the authentication methods supported for registering Dialpad webhooks.
/// </summary>
public enum DialpadWebhookRegistrationAuthenticationType
{
    /// <summary>
    /// No webhook registration authentication method has been selected.
    /// </summary>
    NotConfigured = 0,

    /// <summary>
    /// Use a Dialpad Admin API key to register the webhook.
    /// </summary>
    ApiKey = 1,

    /// <summary>
    /// Use the current Orchard user's connected Dialpad OAuth account to register the webhook.
    /// </summary>
    OAuth2 = 2,
}
