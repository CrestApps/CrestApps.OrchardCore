using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Memberships;
using CrestApps.OrchardCore.Orders.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Memberships",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = MembershipsConstants.Feature,
    Name = "Memberships",
    Description = "Sells memberships: a product whose purchase puts the buyer in roles for a year, a month or for good, so content gated by those roles with Content Access Control opens to them. A refund, a cancellation or the end date takes them out again, and buying again adds time.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        OrderConstants.Features.Orders,
        "OrchardCore.Roles",
    ]
)]
