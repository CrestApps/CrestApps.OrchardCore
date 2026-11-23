using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Marketing;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Outreach;
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
    Description = "Markets to the store's customers: what each has bought, segments of customers by it, such as lapsed buyers, and email campaigns to a segment's customers who accept marketing, with an unsubscribe link to the site's e-mail preferences page. Campaigns are sent by e-mail through the outreach engine. While the Mailing Lists feature is on, a campaign can go to a mailing list's subscribed members instead of a segment.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        ConsentConstants.Features.Consent,
        CustomersConstants.Features.Customers,
        OrderConstants.Features.Orders,
        OrchardCoreFeatureIds.Email,
        OutreachConstants.Feature,
    ]
)]
