using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Resources;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Content Transfer",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Provides bulk content import and export with pluggable file format support.",
    Category = "Content Management",
    Dependencies =
    [
        OrchardCoreFeatureIds.Resources,
        ResourcesConstants.Feature.Area,
    ]
)]
