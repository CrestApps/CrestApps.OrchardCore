namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// The answer to an authentication request: an id token to post to the redirect URI, an error to post there, or an error
/// shown to the person when the redirect URI cannot be trusted.
/// </summary>
public sealed class LtiAuthorizationResult
{
    /// <summary>
    /// Gets or sets the redirect URI to post to, or <see langword="null"/> when the client or the redirect URI is not
    /// valid, so nothing may be posted.
    /// </summary>
    public string RedirectUri { get; set; }

    /// <summary>
    /// Gets or sets the signed id token, when the request succeeded.
    /// </summary>
    public string IdToken { get; set; }

    /// <summary>
    /// Gets or sets the state to echo.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the OAuth 2 error code, such as <c>invalid_request</c> or <c>login_required</c>.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets a description of the error.
    /// </summary>
    public string ErrorDescription { get; set; }

    /// <summary>
    /// Gets whether the request succeeded.
    /// </summary>
    public bool Succeeded => string.IsNullOrEmpty(Error) && !string.IsNullOrEmpty(IdToken);
}
