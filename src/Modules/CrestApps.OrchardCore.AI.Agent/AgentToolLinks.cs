using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Builds the address of an admin page for a tool result, so the assistant can offer the page where a person
/// continues or checks the work.
/// </summary>
internal static class AgentToolLinks
{
    /// <summary>
    /// Gets the address of an action of an admin controller.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="area">The ID of the module that owns the controller.</param>
    /// <param name="controller">The name of the controller, without the Controller suffix.</param>
    /// <param name="action">The name of the action.</param>
    /// <param name="values">The other route values of the action, or <see langword="null"/> when it has none.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetAdminPath(IServiceProvider services, string area, string controller, string action, RouteValueDictionary values = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        var routeValues = new RouteValueDictionary(values)
        {
            ["area"] = area,
            ["controller"] = controller,
            ["action"] = action,
        };

        return linkGenerator.GetPathByRouteValues(httpContext, null, routeValues);
    }
}
