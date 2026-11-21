using System.Net;
using System.Net.Http;

namespace CrestApps.OrchardCore.Core.Http;

/// <summary>
/// Follows redirects for a client whose primary handler follows none, as .NET does, except that a request's credentials
/// never reach another origin. .NET drops only the <c>Authorization</c> header on a redirect and keeps every other
/// header, such as an API key header a connection is configured with, and it sends a request's body again on a 307 or
/// 308. Here a redirect to another origin keeps only the headers that describe what the client accepts, and a redirect
/// to another origin that would send the body again (a token request with a client secret) is not followed: the
/// redirect response is returned. A redirect from https to http is not followed either, as in .NET.
/// </summary>
public sealed class CrossOriginRedirectHandler : DelegatingHandler
{
    /// <summary>
    /// The most redirects followed for one request, as many as .NET follows by default.
    /// </summary>
    public const int MaxRedirects = 50;

    // The request headers that may go to another origin: none of them carries a credential.
    private static readonly HashSet<string> _crossOriginHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accept",
        "Accept-Charset",
        "Accept-Encoding",
        "Accept-Language",
        "Cache-Control",
        "Pragma",
        "User-Agent",
    };

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var origin = request.RequestUri;
        var response = await base.SendAsync(request, cancellationToken);

        for (var redirects = 0; redirects < MaxRedirects; redirects++)
        {
            var target = GetRedirectTarget(response, request.RequestUri);

            if (target is null)
            {
                return response;
            }

            var becomesGet = BecomesGet(response.StatusCode, request.Method);
            var crossOrigin = !IsSameOrigin(origin, target);

            if (crossOrigin && !becomesGet && request.Content is not null)
            {
                return response;
            }

            response.Dispose();

            request.RequestUri = target;
            request.Headers.Authorization = null;

            if (becomesGet)
            {
                request.Method = HttpMethod.Get;
                request.Content = null;
                request.Headers.TransferEncodingChunked = false;
            }

            if (crossOrigin)
            {
                foreach (var name in request.Headers.Select(header => header.Key).Where(name => !_crossOriginHeaders.Contains(name)).ToList())
                {
                    request.Headers.Remove(name);
                }
            }

            response = await base.SendAsync(request, cancellationToken);
        }

        return response;
    }

    // Where a redirect response sends the request, or null when it is not a redirect that is followed.
    private static Uri GetRedirectTarget(HttpResponseMessage response, Uri requestUri)
    {
        if (response.StatusCode is not (HttpStatusCode.MultipleChoices or HttpStatusCode.MovedPermanently or HttpStatusCode.Found or
            HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
        {
            return null;
        }

        var location = response.Headers.Location;

        if (location is null)
        {
            return null;
        }

        if (!location.IsAbsoluteUri)
        {
            location = new Uri(requestUri, location);
        }

        if (location.Scheme != Uri.UriSchemeHttp && location.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return requestUri.Scheme == Uri.UriSchemeHttps && location.Scheme == Uri.UriSchemeHttp ? null : location;
    }

    // A 303 turns any request but HEAD into a GET, and a 300, 301 or 302 turns a POST into one, as browsers and .NET do.
    private static bool BecomesGet(HttpStatusCode statusCode, HttpMethod method)
        => statusCode switch
        {
            HttpStatusCode.SeeOther => method != HttpMethod.Get && method != HttpMethod.Head,
            HttpStatusCode.MultipleChoices or HttpStatusCode.MovedPermanently or HttpStatusCode.Found => method == HttpMethod.Post,
            _ => false,
        };

    private static bool IsSameOrigin(Uri first, Uri second)
        => string.Equals(first.Scheme, second.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(first.IdnHost, second.IdnHost, StringComparison.OrdinalIgnoreCase) &&
            first.Port == second.Port;
}
