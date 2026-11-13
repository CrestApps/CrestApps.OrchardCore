using CrestApps.OrchardCore;
using CrestApps.OrchardCore.SiteResources;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Site Resources",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = SiteResourcesConstants.Features.SiteResources,
    Name = "Site Resources",
    Description = "Stylesheets and scripts kept in the site, minified and served from their own addresses, and added to every page of the front end or the admin, or only to the pages whose templates ask for them by name.",
    Category = "Theming"
)]
