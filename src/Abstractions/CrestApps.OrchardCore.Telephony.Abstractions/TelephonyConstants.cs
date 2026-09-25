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
    /// The data protection purpose used to encrypt conversation recording media at rest in the default local
    /// recording media store.
    /// </summary>
    public const string RecordingMediaProtectorPurpose = "CrestApps.OrchardCore.Telephony.RecordingMedia";

    /// <summary>
    /// The tenant-scoped application-data folder name under which the default local recording media store
    /// persists encrypted recordings.
    /// </summary>
    public const string RecordingMediaFolderName = "RecordingMedia";

    /// <summary>
    /// What a soft-phone client tells the server it can do, once it has registered. A client that reports nothing is
    /// treated as able to do none of these, so a client that predates a capability keeps working as it did.
    /// </summary>
    public static class SoftPhoneClientCapabilities
    {
        /// <summary>
        /// The client recognizes an incoming provider leg rung for a Contact Center offer that is still ringing
        /// on screen, holds it without ringing it as a separate call, and answers it when the agent accepts the
        /// offer (anywhere) or hangs it up when they decline.
        /// </summary>
        public const string HeldOfferLeg = "held-offer-leg";

        /// <summary>
        /// The client answers, without ringing, the leg the platform rings back to it for a number it just dialed from
        /// the keypad (see <see cref="Models.TelephonyCapabilities.BridgedDial"/>), and shows that call as the one it
        /// placed.
        /// </summary>
        public const string BridgedDialLeg = "bridged-dial-leg";

        /// <summary>
        /// Every capability a client may report. Anything else a client sends is ignored.
        /// </summary>
        public static readonly IReadOnlyCollection<string> All = [HeldOfferLeg, BridgedDialLeg];
    }

    /// <summary>
    /// Machine-readable reasons a telephony operation failed, carried on <see cref="Models.TelephonyResult.ErrorCode"/>
    /// for a caller that acts on the reason rather than only showing it.
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>
        /// The provider could not connect a keypad dial through the agent's own browser leg, and no leg of the call was
        /// created, so the soft phone may place the call itself.
        /// </summary>
        public const string BridgeUnavailable = "bridge-unavailable";
    }

    /// <summary>
    /// Contains metadata keys that have provider-neutral command semantics.
    /// </summary>
    public static class RequestMetadata
    {
        /// <summary>
        /// Identifies a stable command that providers should use for idempotent execution when supported.
        /// </summary>
        public const string IdempotencyKey = "idempotencyKey";

        /// <summary>
        /// Identifies the monotonic fence token associated with an idempotent provider command.
        /// </summary>
        public const string FenceToken = "commandFenceToken";

        /// <summary>
        /// Identifies the authenticated user placing a soft phone command. Providers that deliver audio to a
        /// per-user browser endpoint (such as Telnyx WebRTC) use it to resolve the caller's live soft-phone
        /// registration so an outbound call can be bridged to their browser.
        /// </summary>
        public const string SoftPhoneUserId = "softPhoneUserId";

        /// <summary>
        /// Identifies the credential the dialing soft phone is registered on, sent with a keypad dial the soft phone
        /// asks the provider to connect through its own browser leg (see
        /// <see cref="Models.TelephonyCapabilities.BridgedDial"/>). The provider rings that credential only when it
        /// belongs to <see cref="SoftPhoneUserId"/>, is live, is registered and reported
        /// <see cref="SoftPhoneClientCapabilities.BridgedDialLeg"/>.
        /// </summary>
        public const string SoftPhoneCredentialId = "softPhoneCredentialId";

        /// <summary>
        /// Identifies the hub connection a soft phone command came from, stamped by the server (never taken from the
        /// client). A provider that rings the caller's own phone back -- a warm transfer's consult -- prefers the
        /// credential registered from this connection when the request names no live credential of its own.
        /// </summary>
        public const string SoftPhoneConnectionId = "softPhoneConnectionId";

        /// <summary>
        /// The display name of the user placing a soft phone command, stamped by the server (never taken from the
        /// client) so a colleague a call is handed to can be told who handed it over.
        /// </summary>
        public const string SoftPhoneUserDisplayName = "softPhoneUserDisplayName";
    }

    /// <summary>
    /// Contains metadata keys carried on a <see cref="Models.TelephonyCall"/> to convey provider-neutral context
    /// back to the caller (for example, to record call history).
    /// </summary>
    public static class CallMetadata
    {
        /// <summary>
        /// The dialed extension number for an internal extension call, carried so the interaction can be recorded
        /// as an extension call and redialed by extension from the Recent tab.
        /// </summary>
        public const string ExtensionNumber = "extensionNumber";

        /// <summary>
        /// The leg a transfer or consult rings, which the soft phone follows until the transfer is over. The soft
        /// phone names it back when it asks where the consult stands, completes it or cancels it.
        /// </summary>
        public const string ConsultId = "consultId";

        /// <summary>
        /// Where a transfer or consult stands: <see cref="ConsultStatuses.Ringing"/>,
        /// <see cref="ConsultStatuses.Connected"/>, <see cref="ConsultStatuses.Completed"/> or
        /// <see cref="ConsultStatuses.Cancelled"/>.
        /// </summary>
        public const string ConsultStatus = "consultStatus";

        /// <summary>
        /// Whether the transfer or consult is still going on (a boolean).
        /// </summary>
        public const string ConsultLive = "consultLive";

        /// <summary>
        /// Whether the call being transferred has ended -- the caller hung up (a boolean).
        /// </summary>
        public const string ConsultCallEnded = "consultCallEnded";

        /// <summary>
        /// On a consult call, the call the agent is consulting about (the one that will be handed over).
        /// </summary>
        public const string ConsultOf = "consultOf";
    }

    /// <summary>
    /// The states a transfer or consult passes through, as <see cref="CallMetadata.ConsultStatus"/> carries them.
    /// </summary>
    public static class ConsultStatuses
    {
        /// <summary>The destination is being rung.</summary>
        public const string Ringing = "ringing";

        /// <summary>The destination answered; the agent is talking to them and the caller is on hold.</summary>
        public const string Connected = "connected";

        /// <summary>The call was handed to the destination.</summary>
        public const string Completed = "completed";

        /// <summary>The destination did not take the call, or the transfer was called off.</summary>
        public const string Cancelled = "cancelled";
    }

    /// <summary>
    /// Contains the well-known authentication scheme identifiers a telephony provider can use.
    /// </summary>
    public static class AuthenticationSchemes
    {
        /// <summary>
        /// The OAuth 2.0 authorization code scheme, used by providers that authenticate over OAuth 2.0.
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

        /// <summary>
        /// The route of the standalone <c>/softphone</c> page hosted by the soft phone browser extension. Shared
        /// so callers that must recognize the soft phone page (such as the Contact Center agent bar, which never
        /// injects itself there) stay in sync with the route through any rename.
        /// </summary>
        public const string SoftPhonePage = "TelephonySoftPhonePage";
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
        /// The identifier of the shared soft phone client feature. It is enabled by dependency only and
        /// carries the reusable soft phone component, presenter, and resources shared by the widget and the
        /// browser-extension endpoint.
        /// </summary>
        public const string SoftPhoneCore = "CrestApps.OrchardCore.Telephony.SoftPhone.Core";

        /// <summary>
        /// The identifier of the soft phone widget feature. It adds the floating soft phone to the admin
        /// dashboard and provides the placeable front-end Soft Phone widget.
        /// </summary>
        public const string SoftPhone = "CrestApps.OrchardCore.Telephony.SoftPhone";

        /// <summary>
        /// The identifier of the soft phone browser-extension feature. It exposes the standalone
        /// <c>/softphone</c> page and its configuration endpoint that the CrestApps Soft Phone browser
        /// extension hosts.
        /// </summary>
        public const string SoftPhoneExtension = "CrestApps.OrchardCore.Telephony.SoftPhone.Extension";
    }
}
