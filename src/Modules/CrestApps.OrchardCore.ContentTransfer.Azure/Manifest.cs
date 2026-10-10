using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContentTransfer;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Content Transfer - Azure Blob Storage",
    Description = "Stores Content Transfer import and export files in Azure Blob Storage instead of the local file system, so every instance of a scaled-out site reads the same files.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Content Management",
    Dependencies =
    [
        ContentTransferConstants.Feature.ModuleId,
    ]
)]
