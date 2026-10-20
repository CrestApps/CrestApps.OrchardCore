using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Companies;
using CrestApps.OrchardCore.Orders.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Companies",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = CompaniesConstants.Feature,
    Name = "Companies",
    Description = "Sells to businesses: companies with several buyers and their roles, shared locations, payment terms with a deposit and net days, purchase order numbers, quotes, company price lists, and approvals and budgets for buyers' orders.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        OrderConstants.Features.Orders,
    ]
)]
