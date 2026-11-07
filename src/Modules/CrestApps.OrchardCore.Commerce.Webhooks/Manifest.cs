using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Webhooks;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Commerce Webhooks",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

// The commerce modules (Orders, Customers, Inventory, Shipping) declare and publish their events in startups gated on
// this feature; the subscriptions, the delivery outbox and the admin are the generic Webhooks feature it turns on.
[assembly: Feature(
    Id = CommerceConstants.Features.Webhooks,
    Name = "Commerce Webhooks",
    Description = "Tells other systems when orders are placed, paid, canceled, refunded and shipped, customers are created, and stock runs low, through the Webhooks feature: signed JSON posted to the URLs you subscribe, retried until they answer, with a log of every delivery.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        WebhooksConstants.Feature.Webhooks,
    ]
)]
