using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Orders.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Orders",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = OrderConstants.Features.Orders,
    Name = "Orders",
    Description = "Checks carts out through the checkout and records what was bought, with order lists for the store and its customers.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        CartConstants.Features.Carts,
        CheckoutConstants.Features.Area,
    ]
)]
