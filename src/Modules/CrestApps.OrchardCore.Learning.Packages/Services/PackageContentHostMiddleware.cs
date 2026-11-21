using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace CrestApps.OrchardCore.Learning.Packages.Services;

/// <summary>
/// Keeps the package content host and the site apart. With a content host set, the content host answers only the
/// package files and frame pages, and the site's other hosts never answer them; every other request on the content
/// host gets 404, so the site's pages, admin and APIs never run there. A request on the content host loses its
/// <c>Cookie</c> and <c>Authorization</c> headers before authentication runs, so nothing there is ever done as a
/// signed-in person: the launch token in the path is the only credential. Without a content host, the frame page
/// does not exist and everything else is as before.
/// </summary>
internal sealed class PackageContentHostMiddleware
{
    private readonly RequestDelegate _next;

    public PackageContentHostMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, PackageContentHostProvider contentHost)
    {
        var route = PackageContentHost.Route(await contentHost.GetOriginAsync(), context.Request.Host, context.Request.Path);

        if (route == PackageHostRoute.Refused)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.CacheControl = "no-store";

            return;
        }

        if (route == PackageHostRoute.PackageContent)
        {
            context.Request.Headers.Remove(HeaderNames.Cookie);
            context.Request.Headers.Remove(HeaderNames.Authorization);
            context.Items[PackageContentHost.ContentRequestItem] = true;
        }

        await _next(context);
    }
}
