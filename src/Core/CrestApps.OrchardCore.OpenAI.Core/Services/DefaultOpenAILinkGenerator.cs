using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class DefaultOpenAILinkGenerator : IOpenAILinkGenerator
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DefaultOpenAILinkGenerator(
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetContentItemPath(string contentItemId, IDictionary<string, object> metadata)
    {
        if (string.IsNullOrEmpty(contentItemId))
        {
            ArgumentException.ThrowIfNullOrEmpty(contentItemId);
        }

        var routeValues = new RouteValueDictionary()
        {
            { "Area", "OrchardCore.Contents" },
            { "Controller", "Item" },
            { "Action", "Display" },
            { "contentItemId", contentItemId },
        };

        return _linkGenerator.GetPathByRouteValues(_httpContextAccessor.HttpContext, routeName: null, values: routeValues);
    }
}
