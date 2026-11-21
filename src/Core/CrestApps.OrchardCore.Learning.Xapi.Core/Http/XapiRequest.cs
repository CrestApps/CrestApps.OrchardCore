using System.Text;
using System.Text.Json;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Http;

/// <summary>
/// A request to an LRS resource, independent of how it reached the server: the method, the resource, the query
/// parameters, the headers and the body, after the alternate request syntax was unwrapped.
/// </summary>
public sealed class XapiRequest
{
    /// <summary>Gets or sets the HTTP method, in upper case.</summary>
    public string Method { get; set; } = "GET";

    /// <summary>Gets or sets the resource, such as <c>statements</c> or <c>activities/state</c>, without slashes around it.</summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>Gets or sets the query parameters; xAPI parameter names are case sensitive.</summary>
    public IDictionary<string, string> Query { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets the headers, by name ignoring case.</summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the body.</summary>
    public byte[] Body { get; set; } = [];

    /// <summary>Gets or sets the path of the LRS endpoint on this server, such as <c>/learning/xapi</c>, which the
    /// <c>more</c> links of statement results start with.</summary>
    public string EndpointPath { get; set; } = "/" + XapiConstants.RoutePrefix;

    /// <summary>Gets or sets whether the request used the alternate request syntax.</summary>
    public bool Alternate { get; set; }

    /// <summary>Gets the content type header, or an empty string.</summary>
    public string ContentType => Header("Content-Type");

    /// <summary>Gets the media type of the content type, in lower case, without its parameters.</summary>
    public string MediaType
    {
        get
        {
            var contentType = ContentType;
            var separator = contentType.IndexOf(';');

            return (separator < 0 ? contentType : contentType[..separator]).Trim().ToLowerInvariant();
        }
    }

    /// <summary>
    /// Gets a header, or an empty string.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <returns>The value.</returns>
    public string Header(string name)
        => Headers.TryGetValue(name, out var value) ? value ?? string.Empty : string.Empty;

    /// <summary>
    /// Gets a query parameter, or <see langword="null"/>.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The value.</returns>
    public string Param(string name)
        => Query.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    /// Makes a request with a JSON body, for tests and for the LRS's own calls.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="resource">The resource.</param>
    /// <param name="body">The body, serialized as JSON unless it is a string or bytes.</param>
    /// <param name="query">The query parameters.</param>
    /// <returns>The request.</returns>
    public static XapiRequest Create(string method, string resource, object body = null, IDictionary<string, string> query = null)
    {
        var request = new XapiRequest
        {
            Method = method,
            Resource = resource,
        };

        if (query is not null)
        {
            foreach (var (name, value) in query)
            {
                request.Query[name] = value;
            }
        }

        request.Headers[XapiConstants.Headers.Version] = XapiConstants.Version;

        if (body is not null)
        {
            request.Body = body switch
            {
                byte[] bytes => bytes,
                string text => Encoding.UTF8.GetBytes(text),
                _ => JsonSerializer.SerializeToUtf8Bytes(body),
            };
            request.Headers["Content-Type"] = "application/json";
        }

        return request;
    }
}

/// <summary>
/// The answer of an LRS resource, written to the HTTP response by the endpoint.
/// </summary>
public sealed class XapiResponse
{
    /// <summary>Gets or sets the HTTP status.</summary>
    public int Status { get; set; } = 200;

    /// <summary>Gets or sets the headers.</summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the content type of the body.</summary>
    public string ContentType { get; set; }

    /// <summary>Gets or sets the body.</summary>
    public byte[] Body { get; set; } = [];

    /// <summary>Gets the body as text.</summary>
    public string Text => Encoding.UTF8.GetString(Body);

    /// <summary>
    /// Makes a response with no body.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The response.</returns>
    public static XapiResponse Empty(int status = 204)
        => new() { Status = status };

    /// <summary>
    /// Makes a JSON response.
    /// </summary>
    /// <param name="value">The value, or JSON text.</param>
    /// <param name="status">The status.</param>
    /// <returns>The response.</returns>
    public static XapiResponse Json(object value, int status = 200)
        => new()
        {
            Status = status,
            ContentType = "application/json; charset=utf-8",
            Body = value is string text ? Encoding.UTF8.GetBytes(text) : JsonSerializer.SerializeToUtf8Bytes(value),
        };

    /// <summary>
    /// Makes an error response with a message in a JSON body, so that no status code page replaces it.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <param name="message">Why.</param>
    /// <returns>The response.</returns>
    public static XapiResponse Error(int status, string message)
        => Json(new Dictionary<string, object> { ["error"] = true, ["message"] = message }, status);
}
