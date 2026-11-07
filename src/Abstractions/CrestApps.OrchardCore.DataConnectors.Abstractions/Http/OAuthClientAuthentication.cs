namespace CrestApps.OrchardCore.DataConnectors.Http;

/// <summary>
/// How a client authenticates to a token endpoint.
/// </summary>
public enum OAuthClientAuthentication
{
    /// <summary>
    /// The client identifier and secret in a Basic authorization header (<c>client_secret_basic</c>).
    /// </summary>
    BasicHeader,

    /// <summary>
    /// The client identifier and secret in the request body (<c>client_secret_post</c>).
    /// </summary>
    RequestBody,
}
