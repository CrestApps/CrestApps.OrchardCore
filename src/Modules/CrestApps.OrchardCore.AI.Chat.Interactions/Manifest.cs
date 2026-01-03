using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.SignalR.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Chat Interactions",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AIConstants.Feature.ChatInteractions,
    Name = "AI Chat Interactions",
    Description = "Provides ad-hoc AI chat interactions with configurable parameters without predefined profiles.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        "OrchardCore.Liquid",
        "CrestApps.OrchardCore.Resources",
        AIConstants.Feature.ChatCore,
        SignalRConstants.Feature.Area,
        AIConstants.Feature.Area,
    ]
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
