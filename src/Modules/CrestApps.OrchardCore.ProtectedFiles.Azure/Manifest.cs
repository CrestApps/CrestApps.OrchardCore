using CrestApps.OrchardCore;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Protected Files - Azure Blob Storage",
    Description = "Stores protected files (learning packages, coursework submissions, xAPI attachments, learning evidence, QTI feedback images and digital downloads) in Azure Blob Storage instead of the local file system, so every instance of a scaled-out site reads the same files.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Content Management"
)]
