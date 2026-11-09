namespace CrestApps.OrchardCore.DataConnectors.Http;

/// <summary>
/// The kinds of authentication of HTTP-based connections.
/// </summary>
public enum HttpAuthenticationType
{
    /// <summary>
    /// No authentication.
    /// </summary>
    None,

    /// <summary>
    /// An API key in a header or a query parameter.
    /// </summary>
    ApiKey,

    /// <summary>
    /// A user name and a password, sent with Basic authentication.
    /// </summary>
    Basic,

    /// <summary>
    /// A fixed bearer token.
    /// </summary>
    Bearer,

    /// <summary>
    /// A token from an OAuth 2.0 token endpoint.
    /// </summary>
    OAuth2,

    /// <summary>
    /// A token from the token endpoint an OpenID Connect issuer's discovery document names.
    /// </summary>
    OpenIdConnect,
}
