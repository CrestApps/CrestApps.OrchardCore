namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a request to add an internal extension into an active call as a conference participant. The
/// extension is resolved to an on-platform target user; the provider rings that user and joins their leg to the
/// existing conversation.
/// </summary>
public sealed class ExtensionConferenceRequest
{
    /// <summary>
    /// Gets or sets a reference to the active call (or existing conference) the extension is added to.
    /// </summary>
    public CallReference ActiveCall { get; set; }

    /// <summary>
    /// Gets or sets the dialed extension number, as entered on the soft phone.
    /// </summary>
    public string Extension { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the on-platform user the extension resolves to. It is populated by the
    /// telephony service from the extension registry before the provider is invoked; a provider maps this user
    /// to its own live endpoint.
    /// </summary>
    public string TargetUserId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the target user, used for the callee's incoming-call context.
    /// </summary>
    public string TargetDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user initiating the conference add.
    /// </summary>
    public string CallerUserId { get; set; }

    /// <summary>
    /// Gets or sets an optional collection of provider-specific metadata associated with the request.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; }
}
