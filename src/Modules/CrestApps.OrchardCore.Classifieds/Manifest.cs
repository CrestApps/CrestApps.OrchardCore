using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Classifieds;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Orders.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Classifieds",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ClassifiedsConstants.Feature,
    Name = "Classifieds",
    Description = "Lets visitors post listings from the site, such as ads, directory entries or properties: moderated before they're shown, running for a term and then renewed, with a place for a search by distance, and paid upgrades that feature them or bump them to the top.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        OrderConstants.Features.Orders,
    ]
)]
