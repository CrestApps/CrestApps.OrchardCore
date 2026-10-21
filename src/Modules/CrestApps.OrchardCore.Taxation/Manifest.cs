using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
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
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Id = TaxationConstants.Feature.TaxJar,
    Name = "TaxJar",
    Description = "Works out sales tax with TaxJar for orders to the countries you choose, from each address's combined rate, in place of your own tax rules.",
    Category = "Commerce",
    Dependencies =
    [
        TaxationConstants.Feature.Taxation,
    ]
)]
