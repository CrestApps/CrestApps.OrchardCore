namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Defines the well-known authentication scheme identifiers a telephony provider can use. Providers
/// may also define their own scheme identifiers, which keeps the soft phone authentication extensible.
/// </summary>
public static class TelephonyAuthenticationSchemes
{
    /// <summary>
    /// The OAuth 2.0 authorization code scheme, used by providers such as DialPad.
    /// </summary>
    public const string OAuth2 = "oauth2";
}
