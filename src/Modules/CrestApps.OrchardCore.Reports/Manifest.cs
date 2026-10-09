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
    Category = "Reporting",
    Dependencies =
    [
        "CrestApps.OrchardCore.Resources",
        "OrchardCore.Users",
    ]
)]

[assembly: Feature(
    Id = ReportsConstants.DesignerFeature,
    Name = "Report Designer",
    Description = "Lets people design their own reports with drag and drop: pick data sets from any data source, join them, add calculated fields, filters, charts, and pivot tables, save reusable views, pin reports to the admin menu, and share reports with people, roles, or expiring links.",
    Category = "Reporting",
    Dependencies =
    [
        ReportsConstants.Feature,
    ]
)]
