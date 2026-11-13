using System.Security.Claims;
using CrestApps.OrchardCore.SiteResources.Services;
using CrestApps.OrchardCore.ThemeDesigner.Storage;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.ThemeDesigner.Endpoints;

/// <summary>
/// Serves a stylesheet or a script of the signed-in designer's draft, to that designer only.
/// </summary>
internal static class DraftResourceEndpoint
{
    public static async Task<IResult> HandleAsync(
        string fileName,
        HttpContext httpContext,
        ThemeDesignDraftStore draftStore)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true ||
            !SiteResourceResponses.TryParseFileName(fileName, out var name, out var type))
        {
            return Results.NotFound();
        }

        var draft = await draftStore.GetAsync(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));
        var resource = draft?.FindResource(name)?.Resource;

        if (resource is null || resource.Type != type)
        {
            return Results.NotFound();
        }

        return SiteResourceResponses.Serve(httpContext, resource, httpContext.Request.Query["v"], isPrivate: true);
    }
}
