using Microsoft.AspNetCore.Http;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Refuses every request to a parent tenant that a page of one of its child tenants made with a script. A child page
/// is same-site with its parent, so the browser would otherwise send the parent's cookie with such a request.
/// </summary>
internal sealed class ParentFetchMetadataMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParentFetchMetadataMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    public ParentFetchMetadataMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    public Task InvokeAsync(HttpContext context, ShellSettings shellSettings)
    {
        if (shellSettings.IsParentTenant() && FetchMetadataPolicy.IsSameSiteSubresource(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            return Task.CompletedTask;
        }

        return _next(context);
    }
}
