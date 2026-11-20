using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Resources;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Agreements",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AgreementsConstants.Features.Agreements,
    Name = "Agreements",
    Description = "Agreements with accounts, such as support or maintenance contracts: their period, status and the hours they cover (a block of hours or an allowance each month, rounded as agreed). Other modules add their terms and report the time spent under an agreement, which shows the hours used and left.",
    Category = "Organization",
    Dependencies =
    [
        AccountsConstants.Feature,
        ResourcesConstants.Feature.Area,
    ]
)]
