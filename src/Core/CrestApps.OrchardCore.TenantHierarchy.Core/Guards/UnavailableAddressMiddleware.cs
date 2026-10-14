using Microsoft.AspNetCore.Http;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// In the Default tenant, answers a request for the address of a hierarchy tenant that is not running, such as a
/// suspended or removed child tenant, with "not found" instead of the platform site.
/// </summary>
internal sealed class UnavailableAddressMiddleware
{
    internal const string Body = "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Site not available</title></head><body style=\"font-family:system-ui,sans-serif;max-width:32rem;margin:15vh auto;padding:0 1rem;color:#333\"><h1 style=\"font-size:1.5rem\">This site is not available</h1><p>It may be paused or no longer exist. If you expected to reach it, contact the people who run it.</p></body></html>";

    private readonly RequestDelegate _next;
    private readonly IShellHost _shellHost;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnavailableAddressMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    /// <param name="shellHost">The shell host, to read the tenant list.</param>
    public UnavailableAddressMiddleware(RequestDelegate next, IShellHost shellHost)
    {
        _next = next;
        _shellHost = shellHost;
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    public async Task InvokeAsync(HttpContext context, ShellSettings shellSettings)
    {
        if (!shellSettings.IsDefaultShell() ||
            !UnavailableAddressRules.IsHierarchyAddress(context.Request.Host.Value, _shellHost.GetAllSettings()))
        {
            await _next(context);

            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsync(Body, context.RequestAborted);
    }
}
