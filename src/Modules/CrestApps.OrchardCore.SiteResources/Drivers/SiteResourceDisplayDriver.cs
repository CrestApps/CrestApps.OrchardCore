using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.SiteResources.Services;
using CrestApps.OrchardCore.SiteResources.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Extensions;

namespace CrestApps.OrchardCore.SiteResources.Drivers;

/// <summary>
/// The row of a site resource in the admin list, and its editor.
/// </summary>
internal sealed class SiteResourceDisplayDriver : DisplayDriver<SiteResource>
{
    private readonly ISiteResourceService _siteResourceService;
    private readonly IExtensionManager _extensionManager;

    internal readonly IStringLocalizer S;

    public SiteResourceDisplayDriver(
        ISiteResourceService siteResourceService,
        IExtensionManager extensionManager,
        IStringLocalizer<SiteResourceDisplayDriver> stringLocalizer)
    {
        _siteResourceService = siteResourceService;
        _extensionManager = extensionManager;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(SiteResource resource, BuildDisplayContext context)
    {
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = resource.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("SiteResource_Fields_SummaryAdmin", resource)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("SiteResource_Description_SummaryAdmin", resource)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrWhiteSpace(resource.Description))),
            View("SiteResource_DefaultTags_SummaryAdmin", resource)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Where:5"),
            View("SiteResource_Buttons_SummaryAdmin", resource).Location("Actions:5"),
            View("SiteResource_DefaultMeta_SummaryAdmin", resource)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5")
        );
    }

    public override IDisplayResult Edit(SiteResource resource, BuildEditorContext context)
        => Initialize<SiteResourceViewModel>("SiteResourceFields_Edit", model =>
        {
            model.IsNew = context.IsNew;
            model.Name = resource.Name;
            model.Description = resource.Description;
            model.Type = resource.Type;
            model.Content = resource.Content;
            model.Minify = context.IsNew || resource.Minify;
            model.Target = resource.Target;
            model.Placement = resource.Placement;
            model.Location = resource.Location;
            model.Dependencies = string.Join(", ", resource.Dependencies ?? []);
            model.Themes = [.. resource.Themes ?? []];

            // Every installed theme, front end and admin, and any theme the resource names that isn't installed here.
            var themes = _extensionManager.GetFeatures()
                .Where(feature => feature.IsTheme())
                .Select(feature => (feature.Id, feature.Name))
                .DistinctBy(theme => theme.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            themes.AddRange(model.Themes
                .Where(id => !themes.Any(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase)))
                .Select(id => (id, id)));

            model.ThemeOptions = themes
                .OrderBy(theme => theme.Name, StringComparer.OrdinalIgnoreCase)
                .Select(theme => new SelectListItem(theme.Name, theme.Id, model.Themes.Contains(theme.Id, StringComparer.OrdinalIgnoreCase)))
                .ToList();
            model.Position = resource.Position;
            model.Enabled = context.IsNew || resource.Enabled;
            model.Url = context.IsNew || string.IsNullOrEmpty(resource.Name) ? null : _siteResourceService.GetUrl(resource);
            model.ServedLength = resource.ServedContent?.Length ?? 0;
            model.TargetOptions =
            [
                new SelectListItem(S["The front end"], nameof(SiteResourceTarget.Frontend)),
                new SelectListItem(S["The admin"], nameof(SiteResourceTarget.Admin)),
                new SelectListItem(S["Both"], nameof(SiteResourceTarget.Everywhere)),
            ];
            model.PlacementOptions =
            [
                new SelectListItem(S["Every page"], nameof(SiteResourcePlacement.Global)),
                new SelectListItem(S["Only pages whose templates ask for it by name"], nameof(SiteResourcePlacement.OnDemand)),
            ];
            model.LocationOptions =
            [
                new SelectListItem(S["At the end of the page"], nameof(SiteResourceLocation.Foot)),
                new SelectListItem(S["In the head"], nameof(SiteResourceLocation.Head)),
            ];
        }).Location("Content:1");

    public override async Task<IDisplayResult> UpdateAsync(SiteResource resource, UpdateEditorContext context)
    {
        var model = new SiteResourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        resource.Name = model.Name?.Trim();
        resource.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        resource.Type = model.Type;
        resource.Content = model.Content;
        resource.Minify = model.Minify;
        resource.Target = model.Target;
        resource.Placement = model.Placement;
        resource.Location = model.Location;
        resource.Dependencies = (model.Dependencies ?? string.Empty)
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        resource.Themes = (model.Themes ?? [])
            .Where(theme => !string.IsNullOrWhiteSpace(theme))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        resource.Position = model.Position;
        resource.Enabled = model.Enabled;

        return Edit(resource, context);
    }
}
