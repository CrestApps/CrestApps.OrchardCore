using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.FileSources;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI File Sources - SFTP",
    Description = "Adds an SFTP connector to File Sources, so a folder on an SFTP server can be ingested into a File AI data source.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Artificial Intelligence - Knowledgebase",
    Dependencies =
    [
        FileSourceConstants.Feature.FileSources,
    ]
)]
