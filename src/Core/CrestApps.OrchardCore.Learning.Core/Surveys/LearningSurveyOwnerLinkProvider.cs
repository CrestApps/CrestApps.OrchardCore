using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Links the admin pages of a course item's survey back to the course item's editor.
/// </summary>
public sealed class LearningSurveyOwnerLinkProvider : ISurveyOwnerLinkProvider
{
    private readonly IContentManager _contentManager;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSurveyOwnerLinkProvider"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public LearningSurveyOwnerLinkProvider(
        IContentManager contentManager,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _contentManager = contentManager;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<SurveyOwnerLink> GetLinkAsync(RecordRef owner, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null || !LearningSurveyContexts.IsContentOwner(owner))
        {
            return null;
        }

        var item = await _contentManager.GetAsync(owner.Id, VersionOptions.Latest);

        if (item is null)
        {
            return null;
        }

        var metadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(item);
        var url = metadata.EditorRouteValues is null ? null : _linkGenerator.GetPathByRouteValues(httpContext, routeName: null, metadata.EditorRouteValues);

        return string.IsNullOrEmpty(url) ? null : new SurveyOwnerLink { Text = item.DisplayText, Url = url };
    }
}
