using CrestApps.Core.AI.Profiles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Represents the content item AI link generator.
/// </summary>
public sealed class ContentItemAILinkGenerator : IAIReferenceLinkResolver
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemAILinkGenerator"/> class.
    /// </summary>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    public ContentItemAILinkGenerator(
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Performs the resolve link operation.
    /// </summary>
    /// <param name="referenceId">The reference id.</param>
    /// <param name="metadata">The metadata.</param>
    public string ResolveLink(string referenceId, IDictionary<string, object> metadata)
    {
        if (string.IsNullOrEmpty(referenceId))
        {
            return null;
        }

        var routeValues = new RouteValueDictionary()
        {
            { "Area", "OrchardCore.Contents" },
            { "Controller", "Item" },
            { "Action", "Display" },
            { "contentItemId", referenceId },
        };

        return _linkGenerator.GetPathByRouteValues(_httpContextAccessor.HttpContext, routeName: null, values: routeValues);
    }
}
