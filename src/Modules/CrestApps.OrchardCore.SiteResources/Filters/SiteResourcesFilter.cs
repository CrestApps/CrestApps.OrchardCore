using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.SiteResources.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Theming;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.SiteResources.Filters;

/// <summary>
/// Defines the site resources offered to the page (its side and its theme) in the request's resource manifest, so
/// templates can ask for the on-demand ones by name, and adds the global ones to the page.
/// </summary>
internal sealed class SiteResourcesFilter : IAsyncResultFilter
{
    private readonly ISiteResourceService _siteResourceService;
    private readonly IResourceManager _resourceManager;
    private readonly IThemeManager _themeManager;

    public SiteResourcesFilter(
        ISiteResourceService siteResourceService,
        IResourceManager resourceManager,
        IThemeManager themeManager)
    {
        _siteResourceService = siteResourceService;
        _resourceManager = resourceManager;
        _themeManager = themeManager;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Only full pages render the layout that outputs the resources.
        if (context.Result is ViewResult or PageResult)
        {
            // The theme the page is rendered with: the admin theme on admin pages, the site's theme elsewhere.
            var theme = await _themeManager.GetThemeAsync();
            var entries = await _siteResourceService.GetPageResourcesAsync(AdminAttribute.IsApplied(context.HttpContext), theme?.Id);

            foreach (var entry in entries)
            {
                Define(entry);
            }

            foreach (var entry in entries.Where(entry => entry.Resource.Placement == SiteResourcePlacement.Global))
            {
                var settings = _resourceManager.RegisterResource(entry.Resource.GetResourceType(), entry.Resource.Name);

                if (entry.Resource.Type == SiteResourceType.Script && entry.Resource.Location == SiteResourceLocation.Head)
                {
                    settings.AtHead();
                }
                else if (entry.Resource.Type == SiteResourceType.Script)
                {
                    settings.AtFoot();
                }
            }
        }

        await next();
    }

    private void Define(SiteResourceEntry entry)
    {
        var resource = entry.Resource;

        var definition = resource.Type == SiteResourceType.Script
            ? _resourceManager.InlineManifest.DefineScript(resource.Name)
            : _resourceManager.InlineManifest.DefineStyle(resource.Name);

        // The address carries the version already, so the resource manager must not append another one.
        definition.SetUrl(entry.Url).ShouldAppendVersion(false);

        if (resource.Dependencies is { Count: > 0 })
        {
            definition.SetDependencies([.. resource.Dependencies]);
        }
    }
}
