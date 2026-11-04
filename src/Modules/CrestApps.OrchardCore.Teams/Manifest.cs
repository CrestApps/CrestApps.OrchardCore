using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Teams;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Teams",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = TeamsConstants.Features.Teams,
    Name = "Teams",
    Description = "One team hierarchy for the site: the organization, regions, sites, departments, teams and crews, each with a time zone and a linked role. Workforce, Contact Center and other modules build on the same teams and add their own settings to the team editor.",
    Category = "Organization"
)]
