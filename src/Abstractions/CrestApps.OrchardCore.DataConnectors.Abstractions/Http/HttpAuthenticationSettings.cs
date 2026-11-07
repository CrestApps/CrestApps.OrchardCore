namespace CrestApps.OrchardCore.DataConnectors.Http;

/// <summary>
/// How the requests of an HTTP-based connection are authenticated. The secrets themselves are stored apart, protected,
/// under the names in <see cref="HttpAuthenticationSecrets"/>.
/// </summary>
public sealed class HttpAuthenticationSettings
{
    /// <summary>
    /// Gets or sets the kind of authentication.
    /// </summary>
    public HttpAuthenticationType Type { get; set; }

    /// <summary>
    /// Gets or sets the name of the header or query parameter that carries an API key.
    /// </summary>
    public string ApiKeyName { get; set; } = "X-API-Key";

    /// <summary>
    /// Gets or sets where an API key is sent.
    /// </summary>
    public HttpApiKeyLocation ApiKeyLocation { get; set; }

    /// <summary>
    /// Gets or sets text put before an API key in its header, such as <c>ApiKey </c>.
    /// </summary>
    public string ApiKeyPrefix { get; set; }

    /// <summary>
    /// Gets or sets the user name of Basic authentication or of the OAuth 2.0 password grant.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the token endpoint of OAuth 2.0.
    /// </summary>
    public string TokenUrl { get; set; }

    /// <summary>
    /// Gets or sets the issuer of OpenID Connect, whose discovery document names the token endpoint.
    /// </summary>
    public string Authority { get; set; }

    /// <summary>
    /// Gets or sets the client identifier of OAuth 2.0 and OpenID Connect.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the scopes asked for, separated by spaces.
    /// </summary>
    public string Scope { get; set; }

    /// <summary>
    /// Gets or sets the audience or resource some token endpoints need, or <see langword="null"/>.
    /// </summary>
    public string Audience { get; set; }

    /// <summary>
    /// Gets or sets the grant used to get a token.
    /// </summary>
    public OAuthGrantType GrantType { get; set; }

    /// <summary>
    /// Gets or sets how the client authenticates to the token endpoint.
    /// </summary>
    public OAuthClientAuthentication ClientAuthentication { get; set; }
}
