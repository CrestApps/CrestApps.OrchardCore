using CrestApps.OrchardCore;
using CrestApps.OrchardCore.SiteResources;
using CrestApps.OrchardCore.ThemeDesigner;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Theme Designer",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ThemeDesignerConstants.Features.ThemeDesigner,
    Name = "Theme Designer",
    Description = "Change the front-end templates, the admin templates and the site's stylesheets and scripts in a draft that only you see while you browse the site, then publish it in one step. The AI Agent's theme design tools write to the same draft.",
    Category = "Theming",
    Dependencies =
    [
        OrchardCoreFeatureIds.Templates,
        SiteResourcesConstants.Features.SiteResources,
    ]
)]
