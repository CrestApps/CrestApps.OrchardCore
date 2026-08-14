using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContentTransfer;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Content Transfer (OpenXml)",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Adds .xlsx spreadsheet support to the Content Transfer module.",
    Category = "Content Management",
    Dependencies =
    [
        ContentTransferConstants.Feature.ModuleId,
    ]
)]
