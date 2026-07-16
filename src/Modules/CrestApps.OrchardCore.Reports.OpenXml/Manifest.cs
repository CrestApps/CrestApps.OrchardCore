using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Reports;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Reports (OpenXml)",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Adds Excel (.xlsx) exports to the Reports framework using Open XML.",
    Category = "Reporting",
    Dependencies =
    [
        ReportsConstants.Feature,
    ]
)]

[assembly: Feature(
    Id = ReportsConstants.OpenXmlFeature,
    Name = "Reports (OpenXml)",
    Description = "Adds Excel (.xlsx) workbook exports to the Reports area using Open XML.",
    Category = "Reporting"
)]
