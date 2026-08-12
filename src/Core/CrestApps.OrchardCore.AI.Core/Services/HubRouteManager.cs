using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Manages SignalR hub route registration and URL generation.
/// For ASP.NET Core applications, the pathPrefix can be used for multi-tenant or sub-path scenarios.
/// </summary>
public sealed class HubRouteManager
{
    private const string DefaultPath = "/Communication/Hub/";

    private readonly string _hubPrefix;
    private readonly Func<string> _siteBaseUrlResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="HubRouteManager"/> class.
    /// </summary>
    /// <param name="pathPrefix">The path prefix.</param>
    /// <param name="siteBaseUrlResolver">The delegate used to site base url resolver.</param>
    public HubRouteManager(
        string pathPrefix = "",
        Func<string> siteBaseUrlResolver = null)
    {
        _hubPrefix = string.IsNullOrEmpty(pathPrefix) ? string.Empty : '/' + pathPrefix.TrimStart('/');
        _siteBaseUrlResolver = siteBaseUrlResolver;
    }

    /// <summary>
    /// Maps hub.
    /// </summary>
    public static void MapHub<T>(IEndpointRouteBuilder builder)
        where T : Hub
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.MapHub<T>(DefaultPath + typeof(T).Name);
    }

    /// <summary>
    /// Gets path by route.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    public string GetPathByRoute(string pattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        return _hubPrefix + '/' + pattern.TrimStart('/');
    }

    /// <summary>
    /// Gets path by hub.
    /// </summary>
    public string GetPathByHub<T>()
        where T : Hub
    {
        return _hubPrefix + DefaultPath + typeof(T).Name;
    }

    /// <summary>
    /// Gets uri by route.
    /// </summary>
    /// <param name="httpContext">The http context.</param>
    /// <param name="pattern">The pattern.</param>
    public string GetUriByRoute(HttpContext httpContext, string pattern)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        return BuildAbsoluteUri(httpContext, $"{_hubPrefix}/{pattern.TrimStart('/')}");
    }

    /// <summary>
    /// Gets uri by hub.
    /// </summary>
    public string GetUriByHub<T>(HttpContext httpContext)
        where T : Hub
    {
        ArgumentNullException.ThrowIfNull(httpContext);

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

