namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// The parts of an HTTP request or response that an HTTP message signature (RFC 9421) covers.
/// </summary>
public sealed class HttpSignatureMessage
{
    /// <summary>
    /// Gets or sets the request method, such as <c>POST</c>. It is used as given (methods are case-sensitive).
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets the URI scheme, used by <c>@scheme</c> and <c>@target-uri</c>. Defaults to <c>https</c>.
    /// </summary>
    public string Scheme { get; set; } = "https";

    /// <summary>
    /// Gets or sets the authority, <c>host[:port]</c>, without a default port. It is lowercased when signed.
    /// </summary>
    public string Authority { get; set; }

    /// <summary>
    /// Gets or sets the absolute path of the target URI. An empty path is signed as <c>/</c>.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets the query with its leading <c>?</c>, or <see langword="null"/> when the target URI has none.
    /// </summary>
    public string Query { get; set; }

    /// <summary>
    /// Gets or sets the status code of a response, or <see langword="null"/> for a request.
    /// </summary>
    public int? Status { get; set; }

    /// <summary>
    /// Gets or sets the header fields, keyed case-insensitively, with the values of repeated fields already
    /// combined into one comma-separated value.
    /// </summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    internal bool TryGetHeader(string name, out string value)
    {
        value = null;

        if (Headers is null)
        {
            return false;
        }

        if (Headers.TryGetValue(name, out value))
        {
            return value is not null;
        }

        foreach (var header in Headers)
        {
            if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase) && header.Value is not null)
            {
                value = header.Value;

                return true;
            }
        }

        return false;
    }
}
