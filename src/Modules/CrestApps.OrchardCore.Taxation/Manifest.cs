using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Taxation;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Taxation",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = TaxationConstants.Feature.Taxation,
    Name = "Taxation",
    Description = "Provides a provider-agnostic, extensible taxation framework and the TaxationPart.",
    Category = "Commerce"
)]
