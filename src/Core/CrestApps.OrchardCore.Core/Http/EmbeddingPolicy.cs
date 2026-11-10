using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace CrestApps.OrchardCore.Core.Http;

/// <summary>
/// Lets trusted origins frame one response: the origins an administrator allows are checked and written in one shape,
/// the response's <c>Content-Security-Policy</c> gets a <c>frame-ancestors</c> directive that names them, and its
/// cookies can be kept in the framing site's partition so the framed page keeps working where a browser refuses
/// cross-site cookies.
/// </summary>
/// <remarks>
/// Every other response keeps the site's anti-framing headers: nothing here runs unless a caller decides that one
/// response may be framed. A <c>frame-ancestors</c> directive already in the site's policy is replaced, because a
/// second policy header would be intersected with the first and could never widen it.
/// </remarks>
public static class EmbeddingPolicy
{
    /// <summary>
    /// The name of the <c>frame-ancestors</c> directive.
    /// </summary>
    public const string FrameAncestorsDirective = "frame-ancestors";

    /// <summary>
    /// The source expression of the page's own origin.
    /// </summary>
    public const string Self = "'self'";

    /// <summary>
    /// Returns an origin an administrator entered in the one shape a browser reports it in
    /// (<c>https://host[:port]</c>, lowercase, no path), or <see langword="null"/> when it is not an origin that may
    /// frame a page: anything but <c>https</c>, except <c>http</c> on a loopback host for development, a path, a query,
    /// a fragment, user information or a wildcard.
    /// </summary>
    /// <param name="value">The origin as entered.</param>
    public static string NormalizeOrigin(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        if (value.Contains('*', StringComparison.Ordinal) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            string.IsNullOrEmpty(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            uri.AbsolutePath != "/")
        {
            return null;
        }

        var secure = uri.Scheme == Uri.UriSchemeHttps;

        if (!secure && (uri.Scheme != Uri.UriSchemeHttp || !IsLoopback(uri)))
        {
            return null;
        }

        var host = uri.HostNameType == UriHostNameType.IPv6 ? $"[{uri.IdnHost.Trim('[', ']')}]" : uri.IdnHost;

        return $"{uri.Scheme}://{host.ToLowerInvariant()}{(uri.IsDefaultPort ? string.Empty : ":" + uri.Port)}";
    }

    /// <summary>
    /// Returns the valid origins of a list, normalized and without duplicates, in the order they were given.
    /// </summary>
    /// <param name="values">The origins as entered.</param>
    public static IReadOnlyList<string> NormalizeOrigins(IEnumerable<string> values)
        => (values ?? [])
            .Select(NormalizeOrigin)
            .Where(origin => origin is not null)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Returns whether an origin a browser reported is one of the allowed origins.
    /// </summary>
    /// <param name="origin">The origin the browser reported.</param>
    /// <param name="allowed">The allowed origins, normalized.</param>
    public static bool IsAllowedOrigin(string origin, IEnumerable<string> allowed)
        => NormalizeOrigin(origin) is { } normalized && (allowed ?? []).Contains(normalized, StringComparer.Ordinal);

    /// <summary>
    /// Returns a <c>frame-ancestors</c> directive that names the sources, or <c>frame-ancestors 'none'</c> when there
    /// are none.
    /// </summary>
    /// <param name="sources">The source expressions: origins, or <see cref="Self"/>.</param>
    public static string BuildFrameAncestors(IEnumerable<string> sources)
    {
        var list = (sources ?? []).Where(source => !string.IsNullOrWhiteSpace(source)).Distinct(StringComparer.Ordinal).ToList();

        return list.Count == 0
            ? $"{FrameAncestorsDirective} 'none'"
            : $"{FrameAncestorsDirective} {string.Join(' ', list)}";
    }

    /// <summary>
    /// Returns a content security policy with its <c>frame-ancestors</c> directive replaced by one that names the
    /// sources, keeping every other directive.
    /// </summary>
    /// <param name="policy">The policy the response already has, or <see langword="null"/>.</param>
    /// <param name="sources">The source expressions that may frame the response.</param>
    public static string MergeFrameAncestors(string policy, IEnumerable<string> sources)
    {
        var directives = (policy ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(directive => !IsFrameAncestors(directive))
            .ToList();

        directives.Add(BuildFrameAncestors(sources));

        return string.Join("; ", directives);
    }

    /// <summary>
    /// Returns a <c>Set-Cookie</c> value that a browser keeps for a page framed by another site: <c>SameSite=None</c>,
    /// <c>Secure</c> and <c>Partitioned</c>, so the cookie is sent from the frame and lives only in the framing site's
    /// partition, apart from the visitor's own cookies for this site.
    /// </summary>
    /// <param name="setCookie">The <c>Set-Cookie</c> value.</param>
    public static string PartitionCookie(string setCookie)
    {
        if (string.IsNullOrWhiteSpace(setCookie))
        {
            return setCookie;
        }

        var parts = setCookie.Split(';', StringSplitOptions.TrimEntries).ToList();
        var result = new List<string> { parts[0] };

        foreach (var attribute in parts.Skip(1))
        {
            if (attribute.Length == 0 ||
                attribute.StartsWith("samesite", StringComparison.OrdinalIgnoreCase) ||
                attribute.Equals("secure", StringComparison.OrdinalIgnoreCase) ||
                attribute.Equals("partitioned", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(attribute);
        }

        result.Add("secure");
        result.Add("samesite=none");
        result.Add("partitioned");

        return string.Join("; ", result);
    }

    /// <summary>
    /// Returns whether a request path is the given path or below it.
    /// </summary>
    /// <param name="path">The request path, with the tenant's prefix.</param>
    /// <param name="prefix">The path, with the tenant's prefix.</param>
    public static bool IsWithin(string path, string prefix)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(prefix))
        {
            return false;
        }

        prefix = prefix.TrimEnd('/');

        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            (path.Length == prefix.Length || path[prefix.Length] == '/');
    }

    /// <summary>
    /// Lets the sources frame this response, and, when asked, keeps its cookies in the framing site's partition. The
    /// headers are changed just before the response starts, after everything else set its own.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="sources">The source expressions that may frame it; empty leaves the framing headers alone.</param>
    /// <param name="partitionCookies">Whether to keep its cookies in the framing site's partition. Browsers only
    /// accept those cookies over HTTPS.</param>
    public static void Apply(HttpResponse response, IReadOnlyCollection<string> sources, bool partitionCookies)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.OnStarting(() =>
        {
            var headers = response.Headers;

            if (sources is { Count: > 0 })
            {
                headers.Remove(HeaderNames.XFrameOptions);

                var policies = headers.ContentSecurityPolicy;

                headers.ContentSecurityPolicy = policies.Count == 0
                    ? new StringValues(BuildFrameAncestors(sources))
                    : new StringValues(policies.Select(policy => MergeFrameAncestors(policy, sources)).ToArray());
            }

            if (partitionCookies && headers.SetCookie.Count > 0)
            {
                headers.SetCookie = new StringValues(headers.SetCookie.Select(PartitionCookie).ToArray());
            }

            return Task.CompletedTask;
        });
    }

    private static bool IsFrameAncestors(string directive)
        => directive.StartsWith(FrameAncestorsDirective, StringComparison.OrdinalIgnoreCase) &&
            (directive.Length == FrameAncestorsDirective.Length || char.IsWhiteSpace(directive[FrameAncestorsDirective.Length]));

    private static bool IsLoopback(Uri uri)
        => uri.IsLoopback ||
            string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));
}
