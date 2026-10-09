using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Reports;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Content Reports",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Lets the report designer use content types, such as customers or orders, as data sets.",
    Category = "Reporting"
)]

[assembly: Feature(
    Id = ReportsConstants.ContentsFeature,
    Name = "Content Reports",
    Description = "Lets the report designer use content types, such as customers or orders, as data sets.",
    Category = "Reporting",
    Dependencies =
    [
        ReportsConstants.DesignerFeature,
        "OrchardCore.Contents",
    ]
)]
