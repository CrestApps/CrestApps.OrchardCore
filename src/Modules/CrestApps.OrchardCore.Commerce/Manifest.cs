using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Commerce",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Commerce",
    Id = CommerceConstants.Features.Area,
    Description = "Registers the shared Commerce admin menu and its icon for commerce-related modules.",
    Category = "Commerce",
    EnabledByDependencyOnly = true
)]
