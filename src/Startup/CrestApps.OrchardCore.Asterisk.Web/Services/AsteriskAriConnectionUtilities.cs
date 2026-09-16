using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace CrestApps.OrchardCore.Asterisk.Web.Services;

internal static class AsteriskAriConnectionUtilities
{
    public static bool IsConfigured(AsteriskWebOptions options)
        => options is not null &&
            !string.IsNullOrWhiteSpace(options.AsteriskBaseUrl) &&
            !string.IsNullOrWhiteSpace(options.AsteriskUserName) &&
            !string.IsNullOrWhiteSpace(options.AsteriskPassword);

    public static Uri CreateBaseUri(string baseUrl)
    {
        var builder = new UriBuilder(baseUrl);

        if (string.IsNullOrEmpty(builder.Path) || builder.Path == "/")
        {
            builder.Path = "/ari/";
        }
        else if (builder.Path[builder.Path.Length - 1] != '/')
        {
            builder.Path += "/";
        }

        return builder.Uri;
    }

    public static void ApplyBasicAuthentication(HttpClient client, AsteriskWebOptions options)
    {
        client.BaseAddress = CreateBaseUri(options.AsteriskBaseUrl);
        client.DefaultRequestHeaders.ConnectionClose = true;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.AsteriskUserName}:{options.AsteriskPassword}")));
    }

    public static Uri CreateEventsUri(AsteriskWebOptions options)
    {
        var baseUri = CreateBaseUri(options.AsteriskBaseUrl);
        var builder = new UriBuilder(baseUri)
        {
            Scheme = string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? "wss"
                : "ws",
            Path = $"{baseUri.AbsolutePath.TrimEnd('/')}/events",
        };

        builder.Query = QueryHelpers.AddQueryString(
            string.Empty,
            new Dictionary<string, string>
            {
                ["app"] = options.AsteriskApplicationName,
                ["api_key"] = $"{options.AsteriskUserName}:{options.AsteriskPassword}",
                // Keep the listener scoped to its configured ARI app instead of the global PBX event stream.
                ["subscribeAll"] = bool.FalseString.ToLowerInvariant(),
            }).TrimStart('?');

        return builder.Uri;
    }

    public static Uri CreateEventsUriForLogging(AsteriskWebOptions options)
    {
        var baseUri = CreateBaseUri(options.AsteriskBaseUrl);
        var builder = new UriBuilder(baseUri)
        {
            Scheme = string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? "wss"
                : "ws",
            Path = $"{baseUri.AbsolutePath.TrimEnd('/')}/events",
        };

        builder.Query = QueryHelpers.AddQueryString(
            string.Empty,
            new Dictionary<string, string>
            {
                ["app"] = options.AsteriskApplicationName,
                // Keep logging output aligned with the tenant-scoped ARI app subscription used at runtime.
                ["subscribeAll"] = bool.FalseString.ToLowerInvariant(),
            }).TrimStart('?');

        return builder.Uri;
    }
}
