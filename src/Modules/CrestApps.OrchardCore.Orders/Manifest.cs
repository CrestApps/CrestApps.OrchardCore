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

        // The item selector the coupon editor picks products and categories with.
        "CrestApps.OrchardCore.Resources",
    ]
)]

[assembly: Feature(
    Id = OrderConstants.Features.GiftCards,
    Name = "Gift Cards",
    Description = "Sells gift cards and lets buyers pay part or all of an order with one, under Commerce > Gift cards.",
    Category = "Commerce",
    Dependencies =
    [
        OrderConstants.Features.Orders,
        "OrchardCore.Title",
    ]
)]

[assembly: Feature(
    Id = OrderConstants.Features.Returns,
    Name = "Returns",
    Description = "Lets buyers ask to return items of their orders, for a refund or an exchange, and the store approve, receive, restock and refund them under Commerce > Returns.",
    Category = "Commerce",
    Dependencies =
    [
        OrderConstants.Features.Orders,
    ]
)]

[assembly: Feature(
    Name = "Orders API",
    Id = OrderConstants.Features.Api,
    Description = "Lets a headless storefront or app check out the cart, and the buyer see their orders and ask for returns, over the commerce JSON API; staff list, refund and cancel orders. Enable OpenAPI to document it.",
    Category = "Commerce",
    Dependencies =
    [
        OrderConstants.Features.Orders,
        CartConstants.Features.Api,
        CheckoutConstants.Features.Api,
    ]
)]
