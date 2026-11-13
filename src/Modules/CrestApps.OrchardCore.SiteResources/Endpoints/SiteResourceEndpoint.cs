using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.SiteResources.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.Admin;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.SiteResources.Endpoints;

/// <summary>
/// Serves a published site resource. Front-end resources are public; a resource offered only to the admin is served
/// to people who may open the admin.
/// </summary>
internal static class SiteResourceEndpoint
{
    public static async Task<IResult> HandleAsync(
        string fileName,
        HttpContext httpContext,
        IDocumentManager<DictionaryDocument<SiteResource>> documentManager,
        IAuthorizationService authorizationService)
    {
        if (!SiteResourceResponses.TryParseFileName(fileName, out var name, out var type))
        {
            return Results.NotFound();
        }

        var document = await documentManager.GetOrCreateImmutableAsync();

        var resource = document.Records.Values.FirstOrDefault(record =>
            record.Enabled &&
            record.Type == type &&
            string.Equals(record.Name, name, StringComparison.OrdinalIgnoreCase));

        if (resource is null)
        {
            return Results.NotFound();
        }

        var adminOnly = resource.Target == SiteResourceTarget.Admin;

        if (adminOnly && !await authorizationService.AuthorizeAsync(httpContext.User, AdminPermissions.AccessAdminPanel))
        {
            return Results.NotFound();
        }

        return SiteResourceResponses.Serve(httpContext, resource, httpContext.Request.Query["v"], isPrivate: adminOnly);
    }
}
