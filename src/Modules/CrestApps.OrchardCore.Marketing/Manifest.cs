using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Marketing;
using CrestApps.OrchardCore.Orders.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Marketing",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = MarketingConstants.Feature,
    Name = "Marketing",
    Description = "Markets to the store's customers: what each has bought, segments of customers by it, such as lapsed buyers, and email campaigns to a segment's customers who accept marketing, with an unsubscribe link.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        CustomersConstants.Features.Customers,
        OrderConstants.Features.Orders,
    ]
)]
