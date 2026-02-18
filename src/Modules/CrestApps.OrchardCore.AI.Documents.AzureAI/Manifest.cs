using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Documents indexing using Azure AI Search",
    Description = "Provides services to index AI Documents in Azure AI Search indexes.",
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
