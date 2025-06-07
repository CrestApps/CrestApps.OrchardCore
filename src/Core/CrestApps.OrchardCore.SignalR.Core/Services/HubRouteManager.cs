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

        return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{_hubPrefix}/{pattern.TrimStart('/')}";
    }

    public string GetUriByHub<T>(HttpContext httpContext)
        where T : Hub
    {
        return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{_hubPrefix}{DefaultPath}{typeof(T).Name}";
    }
}
