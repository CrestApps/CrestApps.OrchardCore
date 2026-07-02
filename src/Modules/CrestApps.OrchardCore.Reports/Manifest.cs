using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Reports;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Reports",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Provides a reusable reporting framework with a shared admin Reports area, extensible filters, and exports.",
    Category = "Reporting"
)]

[assembly: Feature(
    Id = ReportsConstants.Feature,
    Name = "Reports",
    Description = "Adds the admin Reports area, the extensible report filter with a from/to date range, the uniform report renderer, and CSV export. Other modules contribute reports to this area.",
    Category = "Reporting"
)]
