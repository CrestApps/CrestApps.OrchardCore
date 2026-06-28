namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;

/// <summary>
/// Describes the authentication strategy a verification provider uses.
/// </summary>
public enum PhoneNumberVerificationAuthenticationType
{
    /// <summary>
    /// Authentication using a single API key (typically passed as a query string or header value).
    /// </summary>
    ApiKey = 0,

    /// <summary>
    /// HTTP Basic authentication using a username and password.
    /// </summary>
    Basic = 1,
}
