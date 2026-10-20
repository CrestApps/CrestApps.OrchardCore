using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Orders.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Customers",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = CustomersConstants.Features.Customers,
    Name = "Customers",
    Description = "Keeps a record of everyone who buys from the store, with an account or as a guest, and lists them under Commerce > Customers.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        "OrchardCore.Users",
        AddressConstants.Feature.ModuleId,
    ]
)]

[assembly: Feature(
    Id = CustomersConstants.Features.StoreCredit,
    Name = "Store Credit",
    Description = "Gives customers store credit, by hand or as a refund, that pays toward their orders when they're signed in.",
    Category = "Commerce",
    Dependencies =
    [
        CustomersConstants.Features.Customers,
        OrderConstants.Features.Orders,
    ]
)]

[assembly: Feature(
    Id = CustomersConstants.Features.Loyalty,
    Name = "Loyalty Points",
    Description = "Gives signed-in customers points for what they buy, which they spend toward later orders, set up under Settings > Commerce > Rewards.",
    Category = "Commerce",
    Dependencies =
    [
        CustomersConstants.Features.Customers,
        OrderConstants.Features.Orders,
    ]
)]

[assembly: Feature(
    Id = CustomersConstants.Features.Referrals,
    Name = "Referrals",
    Description = "Gives each signed-in customer a referral link: friends who open it can get a discount on their first order, and the customer is rewarded with store credit or points, set up under Settings > Commerce > Referrals.",
    Category = "Commerce",
    Dependencies =
    [
        CustomersConstants.Features.Customers,
        OrderConstants.Features.Orders,
    ]
)]

[assembly: Feature(
    Id = CustomersConstants.Features.CheckoutDetails,
    Name = "Checkout details",
    Description = "Asks buyers for the store's own details at checkout, such as a gift message or a delivery note. Add the fields to the Checkout details content type; what the buyer enters is kept on the order.",
    Category = "Commerce",
    Dependencies =
    [
        CustomersConstants.Features.Customers,
        OrderConstants.Features.Orders,
        "OrchardCore.Contents",
        "OrchardCore.ContentTypes",
    ]
)]

[assembly: Feature(
    Id = CustomersConstants.Features.Api,
    Name = "Customers API",
    Description = "Lets a signed-in buyer read and change their account and addresses, and a checkout take their contact details, store credit and points, over the commerce JSON API; staff list and look up customers. Enable OpenAPI to document it.",
    Category = "Commerce",
    Dependencies =
    [
        CustomersConstants.Features.Customers,
    ]
)]
