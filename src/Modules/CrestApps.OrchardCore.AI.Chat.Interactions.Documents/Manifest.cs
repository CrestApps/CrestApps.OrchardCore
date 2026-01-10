using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Chat Interactions - Documents",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AIConstants.Feature.ChatDocuments,
    Name = "AI Chat Interactions - Documents",
    Description = "Extends the ad-hoc AI chat interactions with a way to upload documents and chat against uploaded documents.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.ChatInteractions,
        "OrchardCore.Indexing",
    ]
)]
