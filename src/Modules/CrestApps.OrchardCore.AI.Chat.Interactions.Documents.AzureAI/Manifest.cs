using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Chat Interactions - Documents (Azure AI)",
    Description = "Adds Azure AI Search support for chat interaction document embeddings and indexing.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Artificial Intelligence",
    Dependencies =
    [
        ChatInteractionsConstants.Feature.ChatDocuments,
        "OrchardCore.Indexing",
        "OrchardCore.Search.AzureAI",
    ]
)]
