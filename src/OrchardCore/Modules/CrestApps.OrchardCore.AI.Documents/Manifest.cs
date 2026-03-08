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
    Description = "Provides the foundation for document processing, text extraction, and Retrieval-Augmented Generation (RAG) capabilities.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        ChatInteractionsConstants.Feature.ChatInteractions,
        "OrchardCore.Indexing",
    ]
)]

[assembly: Feature(
    Id = ChatInteractionsConstants.Feature.ChatInteractionDocuments,
    Name = "AI Documents for Chat Interactions",
    Description = "Provides document upload and Retrieval-Augmented Generation (RAG) support for AI Chat Interactions.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        ChatInteractionsConstants.Feature.ChatDocuments,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.ProfileDocuments,
    Name = "AI Documents for Profiles",
    Description = "Provides document upload and Retrieval-Augmented Generation (RAG) support for AI Profiles.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        ChatInteractionsConstants.Feature.ChatDocuments,
        AIConstants.Feature.ChatCore,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.ChatSessionDocuments,
    Name = "AI Documents for Chat Sessions",
    Description = "Provides document upload and Retrieval-Augmented Generation (RAG) support for AI Chat Sessions and Widgets.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        ChatInteractionsConstants.Feature.ChatDocuments,
        AIConstants.Feature.Chat,
    ]
)]
