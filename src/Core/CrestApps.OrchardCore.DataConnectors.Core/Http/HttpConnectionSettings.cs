namespace CrestApps.OrchardCore.DataConnectors.Http;

/// <summary>
/// How an HTTP-based connection reaches its server: the base URL, the authentication and extra headers.
/// </summary>
public sealed class HttpConnectionSettings
{
    /// <summary>
    /// Gets or sets the absolute base URL every request of the connection starts with.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets how requests are authenticated.
    /// </summary>
    public HttpAuthenticationSettings Authentication { get; set; } = new();

    /// <summary>
    /// Gets or sets headers sent with every request, such as an API version.
    /// </summary>
    public List<HttpHeaderSetting> Headers { get; set; } = [];

    /// <summary>
    /// Gets or sets the seconds one request may take.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 100;
}

/// <summary>
/// A header sent with every request of a connection.
/// </summary>
public sealed class HttpHeaderSetting
{
    /// <summary>
    /// Gets or sets the header's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the header's value.
    /// </summary>
    public string Value { get; set; }
}

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

/// <summary>
/// Where an API key is sent.
/// </summary>
public enum HttpApiKeyLocation
{
    /// <summary>
    /// In a request header.
    /// </summary>
    Header,

    /// <summary>
    /// In a query string parameter.
    /// </summary>
    Query,
}

/// <summary>
/// The grants used to get a token.
/// </summary>
public enum OAuthGrantType
{
    /// <summary>
    /// The client's own credentials (machine to machine).
    /// </summary>
    ClientCredentials,

    /// <summary>
    /// A user's name and password, for servers that still support the resource owner password grant.
    /// </summary>
    Password,
}

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

/// <summary>
/// The names HTTP authentication secrets are stored under on a connection.
/// </summary>
public static class HttpAuthenticationSecrets
{
    /// <summary>
    /// The API key.
    /// </summary>
    public const string ApiKey = "ApiKey";

    /// <summary>
    /// The password of Basic authentication or of the password grant.
    /// </summary>
    public const string Password = "Password";

    /// <summary>
    /// The fixed bearer token.
    /// </summary>
    public const string BearerToken = "BearerToken";

    /// <summary>
    /// The OAuth 2.0 client secret.
    /// </summary>
    public const string ClientSecret = "ClientSecret";
}
