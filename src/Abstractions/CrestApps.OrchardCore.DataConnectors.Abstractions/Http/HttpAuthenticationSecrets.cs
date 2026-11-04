namespace CrestApps.OrchardCore.DataConnectors.Http;

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
