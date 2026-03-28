using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.SignalR.Services;

/// <summary>
/// Manages SignalR hub route registration and URL generation.
/// For ASP.NET Core applications, the pathPrefix can be used for multi-tenant or sub-path scenarios.
/// </summary>
public sealed class HubRouteManager
{
    private const string DefaultPath = "/Communication/Hub/";

    private readonly string _hubPrefix;

    public HubRouteManager(string pathPrefix = "")
    {
        _hubPrefix = string.IsNullOrEmpty(pathPrefix) ? string.Empty : '/' + pathPrefix.TrimStart('/');
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

        return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{_hubPrefix}/{pattern.TrimStart('/')}";
    }

    public string GetUriByHub<T>(HttpContext httpContext)
        where T : Hub
    {
        return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{_hubPrefix}{DefaultPath}{typeof(T).Name}";
    }
}
