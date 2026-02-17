using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Documents",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ChatInteractionsConstants.Feature.ChatDocuments,
    Name = "AI Documents",
    Description = "Provides document processing and RAG capabilities for AI chat interactions and AI profiles.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        ChatInteractionsConstants.Feature.ChatInteractions,
        "OrchardCore.Indexing",
    ]
)]
