using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Reports;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Reports (PDF)",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Adds PDF exports to the Reports framework.",
    Category = "Reporting"
)]

[assembly: Feature(
    Id = ReportsConstants.PdfFeature,
    Name = "Reports (PDF)",
    Description = "Adds PDF document exports to the Reports area, with metric cards, styled tables, bars and charts.",
    Category = "Reporting",
    Dependencies =
    [
        ReportsConstants.Feature,
    ]
)]
