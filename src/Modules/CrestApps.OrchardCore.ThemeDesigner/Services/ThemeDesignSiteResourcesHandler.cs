using CrestApps.OrchardCore.SiteResources.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// Gives a designer previewing their draft the draft's stylesheets and scripts instead of the published ones.
/// </summary>
internal sealed class ThemeDesignSiteResourcesHandler : ISiteResourcesHandler
{
    private readonly ThemeDesignPreview _preview;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ThemeDesignSiteResourcesHandler(
        ThemeDesignPreview preview,
        IHttpContextAccessor httpContextAccessor)
    {
        _preview = preview;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task ResolvingAsync(SiteResourcesResolvingContext context)
    {
        var draft = await _preview.GetDraftAsync();

        if (draft is null || draft.Resources.Count == 0)
        {
            return;
        }

        var pathBase = _httpContextAccessor.HttpContext?.Request.PathBase.Value ?? string.Empty;

        foreach (var change in draft.Resources)
        {
            context.Entries.RemoveAll(entry => string.Equals(entry.Resource.Name, change.Name, StringComparison.OrdinalIgnoreCase));

            if (!change.Removed && change.Resource is not null)
            {
                context.Entries.Add(new SiteResourceEntry(
                    change.Resource,
                    SiteResourceService.GetUrl(pathBase, ThemeDesignerConstants.DraftResourcesRoutePrefix, change.Resource)));
            }
        }
    }
}
