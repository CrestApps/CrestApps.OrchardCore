namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// The tool's authentication request to the platform's authorization endpoint (Security Framework 5.1.1.2).
/// </summary>
public sealed class LtiAuthorizationRequest
{
    /// <summary>
    /// Gets or sets <c>scope</c>, which must be <c>openid</c>.
    /// </summary>
    public string Scope { get; set; }

    /// <summary>
    /// Gets or sets <c>response_type</c>, which must be <c>id_token</c>.
    /// </summary>
    public string ResponseType { get; set; }

    /// <summary>
    /// Gets or sets <c>response_mode</c>, which must be <c>form_post</c>.
    /// </summary>
    public string ResponseMode { get; set; }

    /// <summary>
    /// Gets or sets <c>prompt</c>, which must be <c>none</c>.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    /// Gets or sets <c>client_id</c>.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets <c>redirect_uri</c>.
    /// </summary>
    public string RedirectUri { get; set; }

    /// <summary>
    /// Gets or sets <c>login_hint</c>.
    /// </summary>
    public string LoginHint { get; set; }

    /// <summary>
    /// Gets or sets <c>lti_message_hint</c>.
    /// </summary>
    public string LtiMessageHint { get; set; }

    /// <summary>
    /// Gets or sets <c>state</c>, echoed in the response.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets <c>nonce</c>.
    /// </summary>
    public string Nonce { get; set; }
}
