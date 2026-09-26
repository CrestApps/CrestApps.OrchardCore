using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Determines whether the current content admin list request is scoped exclusively to omnichannel contact content
/// types, which is the signal the phone-aware search behavior uses to decide when to engage.
/// </summary>
internal static class OmnichannelContactListScope
{
    /// <summary>
    /// Returns <see langword="true"/> when the request carries a <c>contentTypeId</c> value and every content type it
    /// lists (comma separated) has the <c>OmnichannelContactPart</c> attached; otherwise, <see langword="false"/>.
    /// </summary>
    public static async ValueTask<bool> IsContactOnlyListAsync(
        HttpContext httpContext,
        OmnichannelContentTypeProvider contentTypeProvider)
    {
        if (httpContext is null)
        {
            return false;
        }

        // The content admin list carries the type filter as a route value (…/ContentItems/{contentTypeId}), not a
        // query string.
        if (!httpContext.Request.RouteValues.TryGetValue("contentTypeId", out var routeValue) ||
            routeValue is not string routeContentTypeId)
        {
            return false;
        }

        var requestedContentTypes = routeContentTypeId
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (requestedContentTypes.Count == 0)
        {
            return false;
        }

        var contactContentTypes = await contentTypeProvider.GetContactContentTypesAsync();

        if (contactContentTypes.Count == 0)
        {
            return false;
        }

        var contactContentTypeSet = contactContentTypes as ISet<string>
            ?? new HashSet<string>(contactContentTypes, StringComparer.Ordinal);

        return requestedContentTypes.All(contactContentTypeSet.Contains);
    }
}
