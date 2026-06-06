using CrestApps.OrchardCore;
using CrestApps.OrchardCore.DncRegistry;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "DNC Registry - Azure Blob Storage",
    Description = "Stores uploaded Local DNC Registry files in Azure Blob Storage instead of the local file system.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Compliance",
    Dependencies =
    [
        DncRegistryConstants.Features.Local,
    ]
)]
