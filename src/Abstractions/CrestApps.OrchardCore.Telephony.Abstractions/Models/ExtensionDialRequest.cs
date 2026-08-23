namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a request to call an internal extension. The extension is resolved to an on-platform target user,
/// and the provider connects the caller to that user's live endpoint without routing through the PSTN.
/// </summary>
public sealed class ExtensionDialRequest
{
    /// <summary>
    /// Gets or sets the dialed extension number, as entered on the soft phone.
    /// </summary>
    public string Extension { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the on-platform user the extension resolves to. It is populated by the
    /// telephony service from the extension registry before the provider is invoked; a provider maps this user
    /// to its own live endpoint (for example the user's current browser SIP credential).
    /// </summary>
    public string TargetUserId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the target user, used for the callee's incoming-call context and the
    /// caller's own call history.
    /// </summary>
    public string TargetDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user placing the call. Providers present this as the internal caller
    /// identity on the target's ringing offer.
    /// </summary>
    public string CallerUserId { get; set; }

    /// <summary>
    /// Gets or sets an optional caller identifier to present to the target. When not provided the provider uses
    /// the caller's internal identity or its configured default.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets an optional collection of provider-specific metadata associated with the call.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; }
}
