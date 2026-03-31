using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.SignalR.Core.Services;

public sealed class HubRouteManager
{
    private const string DefaultPath = "/Communication/Hub/";

    private readonly string _hubPrefix;

    public HubRouteManager(ShellSettings shellSettings)
    {
        if (!string.IsNullOrEmpty(shellSettings.RequestUrlPrefix))
        {
            _hubPrefix = '/' + shellSettings.RequestUrlPrefix;
        }
        else
        {
            _hubPrefix = string.Empty;
        }
    }

#pragma warning disable CA1822 // Mark members as static
    public void MapHub<T>(IEndpointRouteBuilder builder)
#pragma warning restore CA1822 // Mark members as static
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

    private static string BuildAbsoluteUri(HttpContext httpContext, string path)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrEmpty(path);

        var requestAbsoluteUri = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{path}";
        if (!TryGetSiteBaseUrl(httpContext, out var siteBaseUrl) ||
            string.IsNullOrWhiteSpace(siteBaseUrl) ||
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

    private static bool TryGetSiteBaseUrl(HttpContext httpContext, out string baseUrl)
    {
        baseUrl = null;

        var siteServiceType =
            Type.GetType("OrchardCore.Settings.ISiteService, OrchardCore.Infrastructure.Abstractions") ??
            Type.GetType("OrchardCore.Settings.ISiteService, OrchardCore.Settings");

        if (siteServiceType is null)
        {
            return false;
        }

        var siteService = httpContext.RequestServices.GetService(siteServiceType);
        var getSiteSettingsAsync = siteServiceType.GetMethod("GetSiteSettingsAsync", Type.EmptyTypes);

        if (siteService is null || getSiteSettingsAsync is null)
        {
            return false;
        }

        if (getSiteSettingsAsync.Invoke(siteService, null) is not Task task)
        {
            return false;
        }

        task.GetAwaiter().GetResult();

        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        baseUrl = result?.GetType().GetProperty("BaseUrl")?.GetValue(result) as string;

        return !string.IsNullOrWhiteSpace(baseUrl);
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
