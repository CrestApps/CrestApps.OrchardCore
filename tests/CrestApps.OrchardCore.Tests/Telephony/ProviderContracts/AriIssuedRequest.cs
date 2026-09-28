namespace CrestApps.OrchardCore.Tests.Telephony.ProviderContracts;

/// <summary>
/// Represents a single HTTP request the production Asterisk REST Interface client issued while replaying recorded
/// provider responses.
/// </summary>
internal sealed class AriIssuedRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AriIssuedRequest"/> class.
    /// </summary>
    /// <param name="operation">The client operation that issued the request.</param>
    /// <param name="httpMethod">The HTTP method of the request.</param>
    /// <param name="path">The request path relative to the ARI base path.</param>
    public AriIssuedRequest(
        string operation,
        string httpMethod,
        string path)
    {
        Operation = operation;
        HttpMethod = httpMethod;
        Path = path;
    }

    /// <summary>
    /// Gets the client operation that issued the request.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Gets the HTTP method of the request.
    /// </summary>
    public string HttpMethod { get; }

    /// <summary>
    /// Gets the request path relative to the ARI base path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the query string parameter names the request carried.
    /// </summary>
    public HashSet<string> QueryParameterNames { get; } = new(StringComparer.Ordinal);
}
