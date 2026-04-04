using CrestApps.AI.Profiles;
using CrestApps.Mvc.Web.Controllers;

namespace CrestApps.Mvc.Web.Services;

public sealed class ArticleAIReferenceLinkResolver : IAIReferenceLinkResolver
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ArticleAIReferenceLinkResolver(
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public string ResolveLink(string referenceId, IDictionary<string, object> metadata)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            return null;
        }

        return _linkGenerator.GetPathByRouteValues(
            _httpContextAccessor.HttpContext,
            ArticlesController.DisplayRouteName,
            new RouteValueDictionary
            {
                ["id"] = referenceId,
            });
    }
}
