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
