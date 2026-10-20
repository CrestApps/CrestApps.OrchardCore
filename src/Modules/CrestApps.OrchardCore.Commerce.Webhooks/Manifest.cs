using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Commerce.Webhooks;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Commerce Webhooks",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = WebhookConstants.Feature,
    Name = "Commerce Webhooks",
    Description = "Tells other systems when orders are placed, paid, canceled, refunded and shipped, customers are created, and stock runs low, by posting signed JSON to the URLs you subscribe, retried until they answer, with a log of every delivery.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
    ]
)]
