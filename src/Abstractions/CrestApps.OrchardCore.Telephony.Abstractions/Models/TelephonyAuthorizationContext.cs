namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Provides the context required to build a provider OAuth authorization request.
/// </summary>
public sealed class TelephonyAuthorizationContext
{
    /// <summary>
    /// Gets or sets the absolute callback URL the provider redirects to after authorization.
    /// </summary>
    public string RedirectUri { get; set; }

    /// <summary>
    /// Gets or sets the opaque state value used to protect the flow against cross-site request forgery.
    /// </summary>
    public string State { get; set; }
}
