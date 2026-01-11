using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Chat Interactions - Documents",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ChatInteractionsConstants.Feature.ChatDocuments,
    Name = "AI Chat Interactions - Documents",
    Description = "Extends the ad-hoc AI chat interactions with a way to upload documents and chat against uploaded documents.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        ChatInteractionsConstants.Feature.ChatInteractions,
        "OrchardCore.Indexing",
    ]
)]
