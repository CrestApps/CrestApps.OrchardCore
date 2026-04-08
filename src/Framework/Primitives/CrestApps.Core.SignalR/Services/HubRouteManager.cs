using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.Core.SignalR.Services;

/// <summary>
/// Manages SignalR hub route registration and URL generation.
/// For ASP.NET Core applications, the pathPrefix can be used for multi-tenant or sub-path scenarios.
/// </summary>
public sealed class HubRouteManager
{
    private const string DefaultPath = "/Communication/Hub/";

    private readonly string _hubPrefix;
    private readonly Func<string> _siteBaseUrlResolver;

    public HubRouteManager(string pathPrefix = "", Func<string> siteBaseUrlResolver = null)
    {
        _hubPrefix = string.IsNullOrEmpty(pathPrefix) ? string.Empty : '/' + pathPrefix.TrimStart('/');
        _siteBaseUrlResolver = siteBaseUrlResolver;
    }

    public static void MapHub<T>(IEndpointRouteBuilder builder)
        where T : Hub
    {
        builder.MapHub<T>(DefaultPath + typeof(T).Name);
    }

    public string GetPathByRoute(string pattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        return _hubPrefix + '/' + pattern.TrimStart('/');
    }

    public string GetPathByHub<T>()
        where T : Hub
    {
        return _hubPrefix + DefaultPath + typeof(T).Name;
    }

    public string GetUriByRoute(HttpContext httpContext, string pattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        return BuildAbsoluteUri(httpContext, $"{_hubPrefix}/{pattern.TrimStart('/')}");
    }

    public string GetUriByHub<T>(HttpContext httpContext)
        where T : Hub
    {
        return BuildAbsoluteUri(httpContext, $"{_hubPrefix}{DefaultPath}{typeof(T).Name}");
    }

    private string BuildAbsoluteUri(HttpContext httpContext, string path)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrEmpty(path);

        var requestAbsoluteUri = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{path}";
        var siteBaseUrl = _siteBaseUrlResolver?.Invoke();
        if (string.IsNullOrWhiteSpace(siteBaseUrl) ||
            !Uri.TryCreate(siteBaseUrl, UriKind.Absolute, out var siteBaseUri))
        {
            return requestAbsoluteUri;
        }

        var relativePath = path;
        var requestPathBaseValue = httpContext.Request.PathBase.Value?.TrimEnd('/');

        if (!string.IsNullOrEmpty(requestPathBaseValue) &&
            relativePath.StartsWith(requestPathBaseValue, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath[requestPathBaseValue.Length..];
        }

        var siteBasePath = siteBaseUri.AbsolutePath.TrimEnd('/');

        if (!string.IsNullOrEmpty(siteBasePath) &&
            siteBasePath != "/" &&
                relativePath.StartsWith(siteBasePath, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath[siteBasePath.Length..];
        }

        return new Uri(EnsureTrailingSlash(siteBaseUri), relativePath.TrimStart('/')).AbsoluteUri;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        if (uri.AbsoluteUri.EndsWith('/'))
        {
            return uri;
        }

        return new Uri(uri.AbsoluteUri + "/", UriKind.Absolute);
    }
}
