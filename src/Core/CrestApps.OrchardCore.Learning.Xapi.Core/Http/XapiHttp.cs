using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Http;

/// <summary>
/// Reads an HTTP request into an <see cref="XapiRequest"/>, unwrapping the alternate request syntax of xAPI 1.0.3, and
/// writes an <see cref="XapiResponse"/> with the version header and the CORS headers every LRS response carries.
/// </summary>
public static class XapiHttp
{
    /// <summary>
    /// The headers a browser may send to the LRS from another origin.
    /// </summary>
    public const string AllowedHeaders = "Authorization, Content-Type, X-Experience-API-Version, If-Match, If-None-Match, X-Requested-With, Accept-Language";

    /// <summary>
    /// The headers a browser may read from an LRS response from another origin.
    /// </summary>
    public const string ExposedHeaders = "ETag, Last-Modified, Content-Type, Content-Length, WWW-Authenticate, X-Experience-API-Version, X-Experience-API-Consistent-Through";

    /// <summary>
    /// The methods the LRS takes.
    /// </summary>
    public const string AllowedMethods = "GET, HEAD, PUT, POST, DELETE, OPTIONS";

    private static readonly string[] _alternateHeaders = ["Authorization", XapiConstants.Headers.Version, "Content-Type", "Content-Length", "If-Match", "If-None-Match"];
    private static readonly string[] _alternateMethods = ["GET", "HEAD", "PUT", "POST", "DELETE"];

    /// <summary>
    /// Reads a request.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="resource">The resource path after the LRS endpoint.</param>
    /// <param name="endpointPath">The path of the LRS endpoint on this server.</param>
    /// <param name="maxBytes">The largest body.</param>
    /// <returns>The request, or the error response when the request cannot be read (400 or 413).</returns>
    public static async Task<(XapiRequest Request, XapiResponse Error)> ReadAsync(HttpContext httpContext, string resource, string endpointPath, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var request = httpContext.Request;

        if (request.ContentLength > maxBytes)
        {
            return (null, XapiResponse.Error(413, $"The request is larger than {maxBytes} bytes."));
        }

        var body = await ReadBodyAsync(request.Body, maxBytes, httpContext.RequestAborted);

        if (body is null)
        {
            return (null, XapiResponse.Error(413, $"The request is larger than {maxBytes} bytes."));
        }

        var xapi = new XapiRequest
        {
            Method = request.Method.ToUpperInvariant(),
            Resource = resource ?? string.Empty,
            EndpointPath = endpointPath,
            Body = body,
        };

        foreach (var (name, value) in request.Headers)
        {
            xapi.Headers[name] = value.ToString();
        }

        foreach (var (name, value) in request.Query)
        {
            xapi.Query[name] = value.ToString();
        }

        if (xapi.Method == "POST" && xapi.Query.TryGetValue("method", out var method))
        {
            return Unwrap(xapi, method);
        }

        return (xapi, null);
    }

    /// <summary>
    /// Unwraps a request in the alternate request syntax: a <c>POST</c> whose only query parameter is <c>method</c>,
    /// with a form body holding the headers, the <c>content</c> and the other parameters. Only writes (<c>PUT</c>,
    /// <c>POST</c> and <c>DELETE</c>) are accepted this way; <c>GET</c> and <c>HEAD</c> are refused.
    /// </summary>
    /// <param name="request">The request as received.</param>
    /// <param name="method">The method it stands for.</param>
    /// <returns>The request it stands for, or why it is refused.</returns>
    public static (XapiRequest Request, XapiResponse Error) Unwrap(XapiRequest request, string method)
    {
        ArgumentNullException.ThrowIfNull(request);

        var target = (method ?? string.Empty).ToUpperInvariant();

        if (Array.IndexOf(_alternateMethods, target) < 0)
        {
            return (null, XapiResponse.Error(400, "The method parameter of the alternate request syntax must be GET, HEAD, PUT, POST or DELETE."));
        }

        // A form can post this syntax from any page as a top-level navigation, carrying a credential as a field. A read
        // through it would render the answer on the site's origin, so reads must send their headers as headers.
        if (target is "GET" or "HEAD")
        {
            return (null, XapiResponse.Error(400, "The alternate request syntax is not accepted for GET and HEAD: send the request with its Authorization and X-Experience-API-Version headers."));
        }

        if (request.Query.Count != 1)
        {
            return (null, XapiResponse.Error(400, "In the alternate request syntax, method is the only query parameter; the others go in the form."));
        }

        if (request.MediaType != "application/x-www-form-urlencoded")
        {
            return (null, XapiResponse.Error(400, "The alternate request syntax sends its form as application/x-www-form-urlencoded."));
        }

        var form = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery("?" + System.Text.Encoding.UTF8.GetString(request.Body));
        var unwrapped = new XapiRequest
        {
            Method = target,
            Resource = request.Resource,
            EndpointPath = request.EndpointPath,
            Alternate = true,
        };

        // The request's own headers that are not about its form, such as Accept-Language, still apply.
        foreach (var (name, value) in request.Headers)
        {
            if (!name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) && !name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                unwrapped.Headers[name] = value;
            }
        }

        foreach (var (name, value) in form)
        {
            var header = _alternateHeaders.FirstOrDefault(candidate => candidate.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (header is not null)
            {
                unwrapped.Headers[header] = value.ToString();
            }
            else if (name == "content")
            {
                unwrapped.Body = System.Text.Encoding.UTF8.GetBytes(value.ToString());
            }
            else
            {
                unwrapped.Query[name] = value.ToString();
            }
        }

        if (unwrapped.Body.Length > 0 && string.IsNullOrEmpty(unwrapped.ContentType))
        {
            unwrapped.Headers["Content-Type"] = "application/json";
        }

        return (unwrapped, null);
    }

    /// <summary>
    /// Writes a response with the version header and the CORS headers.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="response">The response.</param>
    /// <param name="head">Whether the request was a <c>HEAD</c>, which gets the headers without the body.</param>
    /// <returns>A task.</returns>
    public static async Task WriteAsync(HttpContext httpContext, XapiResponse response, bool head)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(response);

        KeepStatusCode(httpContext);

        var http = httpContext.Response;

        http.StatusCode = response.Status;
        AddCommonHeaders(http.Headers);

        foreach (var (name, value) in response.Headers)
        {
            http.Headers[name] = value;
        }

        http.Headers.CacheControl = "no-store";

        if (response.Body.Length == 0 && string.IsNullOrEmpty(response.ContentType))
        {
            return;
        }

        http.ContentType = response.ContentType ?? "application/octet-stream";
        http.ContentLength = response.Body.Length;

        if (!head)
        {
            await http.Body.WriteAsync(response.Body, httpContext.RequestAborted);
        }
    }

    /// <summary>
    /// Answers a CORS preflight: any origin, the xAPI methods and headers, and never credentials. That is safe because
    /// the LRS never takes the sign-in cookie: a page can only reach it with a credential it was given.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    public static void WritePreflight(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        KeepStatusCode(httpContext);

        var http = httpContext.Response;

        http.StatusCode = StatusCodes.Status204NoContent;
        AddCommonHeaders(http.Headers);
        http.Headers.AccessControlAllowMethods = AllowedMethods;
        http.Headers.AccessControlAllowHeaders = AllowedHeaders;
        http.Headers.AccessControlMaxAge = "600";
    }

    private static void AddCommonHeaders(IHeaderDictionary headers)
    {
        headers[XapiConstants.Headers.Version] = XapiConstants.Version;
        headers.AccessControlAllowOrigin = "*";
        headers.AccessControlExposeHeaders = ExposedHeaders;
        headers.XContentTypeOptions = "nosniff";
        headers.Vary = new StringValues("Origin");
    }

    private static void KeepStatusCode(HttpContext httpContext)
    {
        // An error of the LRS has its own JSON body: no status code page replaces it.
        if (httpContext.Features.Get<IStatusCodePagesFeature>() is { } statusCodePages)
        {
            statusCodePages.Enabled = false;
        }
    }

    private static async Task<byte[]> ReadBodyAsync(Stream body, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while ((read = await body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
