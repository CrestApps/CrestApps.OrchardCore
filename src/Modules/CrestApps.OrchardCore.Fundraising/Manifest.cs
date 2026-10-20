using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Fundraising;
using CrestApps.OrchardCore.Orders.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Fundraising",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = FundraisingConstants.Feature,
    Name = "Fundraising",
    Description = "Raises money: a campaign donors give their own amount to, from suggested amounts or their own, in honor of someone if they like, with progress toward a goal on its page, and a tax receipt for each donation, emailed and kept in the donor's account.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        OrderConstants.Features.Orders,
    ]
)]
