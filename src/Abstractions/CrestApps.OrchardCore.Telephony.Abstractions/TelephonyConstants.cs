namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Contains constant values used by the Telephony feature.
/// </summary>
public static class TelephonyConstants
{
    /// <summary>
    /// The identifier of the site settings group used to configure telephony and its providers.
    /// Every telephony provider settings driver must use this group id so the provider settings
    /// appear as tabs on the same telephony settings screen.
    /// </summary>
    public const string SettingsGroupId = "telephony";

    /// <summary>
    /// The data protection purpose used to encrypt user OAuth tokens at rest.
    /// </summary>
    public const string TokenProtectorPurpose = "CrestApps.OrchardCore.Telephony.UserTokens";

    /// <summary>
    /// Contains the well-known authentication scheme identifiers a telephony provider can use.
    /// </summary>
    public static class AuthenticationSchemes
    {
        /// <summary>
        /// The OAuth 2.0 authorization code scheme, used by providers such as DialPad.
        /// </summary>
        public const string OAuth2 = "oauth2";
    }

    /// <summary>
    /// Contains the names of the routes exposed by the Telephony module.
    /// </summary>
    public static class RouteNames
    {
        /// <summary>
        /// The route that starts the provider OAuth connection flow.
        /// </summary>
        public const string OAuthConnect = "TelephonyOAuthConnect";

        /// <summary>
        /// The route the provider redirects to after authorization.
        /// </summary>
        public const string OAuthCallback = "TelephonyOAuthCallback";

        /// <summary>
        /// The route that disconnects the current user from the provider.
        /// </summary>
        public const string OAuthDisconnect = "TelephonyOAuthDisconnect";
    }

    /// <summary>
    /// Contains the feature identifiers exposed by the Telephony module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the core Telephony feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Telephony";

        /// <summary>
        /// The identifier of the soft phone feature.
        /// </summary>
        public const string SoftPhone = "CrestApps.OrchardCore.Telephony.SoftPhone";

    }
}
