using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore;

/// <summary>
/// Provides conventional SignalR hub routes used by CrestApps Orchard Core modules.
/// </summary>
public static class SignalRHubRoutes
{
    private const string DefaultPath = "/Communication/Hub/";

    /// <summary>
    /// Gets the tenant-local route path for the specified SignalR hub type.
    /// </summary>
    /// <typeparam name="T">The SignalR hub type.</typeparam>
    /// <returns>The route path for the hub.</returns>
    public static string GetHubPath<T>()
        where T : Hub
    {
        return DefaultPath + typeof(T).Name;
    }

    /// <summary>
    /// Gets the client URL for the specified SignalR hub type with the current request path base.
    /// </summary>
    /// <typeparam name="T">The SignalR hub type.</typeparam>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The client URL for the hub.</returns>
    public static string GetTenantAwareHubUrl<T>(HttpContext httpContext)
        where T : Hub
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return httpContext.Request.PathBase.Add(new PathString(GetHubPath<T>())).ToUriComponent();
    }
}
