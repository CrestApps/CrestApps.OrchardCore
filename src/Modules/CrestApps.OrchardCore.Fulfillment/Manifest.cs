using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Fulfillment;
using CrestApps.OrchardCore.Orders.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Fulfillment Partners",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = FulfillmentConstants.Features.Fulfillment,
    Name = "Fulfillment Partners",
    Description = "Connects the store to the outside companies that make and ship its products, such as print-on-demand services, under Settings > Commerce > Fulfillment partners. Paid orders are sent to the partner that makes them, from the order page or automatically. Each company is its own feature.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        OrderConstants.Features.Orders,
    ]
)]
