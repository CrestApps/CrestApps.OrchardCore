using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Core;
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
    Description = "Provides the foundation for document processing and AI searching.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        ChatInteractionsConstants.Feature.ChatInteractions,
        "OrchardCore.Indexing",
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.ProfileDocuments,
    Name = "AI Profile Documents",
    Description = "Provides document upload and Retrieval-Augmented Generation (RAG) support for AI Profiles.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        ChatInteractionsConstants.Feature.ChatDocuments,
        AIConstants.Feature.ChatCore,
    ]
)]
