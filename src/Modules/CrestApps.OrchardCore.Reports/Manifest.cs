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
    Id = ReportsConstants.SubscriptionsFeature,
    Name = "Report Subscriptions",
    Description = "Emails reports on a schedule: a report every day, week or month for the period before, such as the sales report every Monday, with its numbers and tables and the report as a file.",
    Category = "Reporting",
    Dependencies =
    [
        ReportsConstants.Feature,
        "OrchardCore.Email",
    ]
)]

[assembly: Feature(
    Id = ReportsConstants.BuilderFeature,
    Name = "Report Builder",
    Description = "Lets people build their own reports with drag and drop: pick data sets from any data source, join them, add calculated fields, filters, charts, and pivot tables, save reusable views, pin reports to the admin menu, and share reports with people, roles, or links.",
    Category = "Reporting",
    Dependencies =
    [
        ReportsConstants.Feature,
        "OrchardCore.DataPipelines.Core",
    ]
)]

[assembly: Feature(
    Id = ReportWidgetConstants.Feature,
    Name = "Report Dashboard Widgets",
    Description = "Shows any report on the admin dashboard as a widget: pick the report, the period (last 30 days, this month, custom dates), what of it to show and how long to keep the result (30 minutes by default) so the dashboard stays quick. Other modules ship ready-made widgets on top of it.",
    Category = "Reporting",
    Dependencies =
    [
        ReportsConstants.Feature,
        "OrchardCore.AdminDashboard",
        "OrchardCore.Contents",
        "OrchardCore.Title",
    ]
)]
