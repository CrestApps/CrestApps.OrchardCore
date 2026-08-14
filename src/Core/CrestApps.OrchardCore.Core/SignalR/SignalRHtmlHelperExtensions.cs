using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore;

/// <summary>
/// Provides Razor helpers for SignalR hubs.
/// </summary>
public static class SignalRHtmlHelperExtensions
{
    /// <summary>
    /// Gets the client URL for the specified SignalR hub type with the current request path base.
    /// </summary>
    /// <typeparam name="T">The SignalR hub type.</typeparam>
    /// <param name="htmlHelper">The HTML helper.</param>
    /// <returns>The client URL for the hub.</returns>
    public static string SignalRHubUrl<T>(this IHtmlHelper htmlHelper)
        where T : Hub
    {
        ArgumentNullException.ThrowIfNull(htmlHelper);

        return SignalRHubRoutes.GetTenantAwareHubUrl<T>(htmlHelper.ViewContext.HttpContext);
    }
}
