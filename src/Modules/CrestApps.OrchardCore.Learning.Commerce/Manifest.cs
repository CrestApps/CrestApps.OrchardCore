using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Commerce;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Products.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning Commerce",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = LearningCommerceConstants.Feature,
    Name = "Learning Commerce",
    Description = "Sells courses and learning paths as products: a paid order enrolls the buyer or starts the path, an account buys seats its admins give to members or invite people to, and a full refund or a cancellation takes it all back.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
        ProductConstants.Feature.Listing,
        OrderConstants.Features.Orders,
        AccountsConstants.MembersFeature,
    ]
)]
