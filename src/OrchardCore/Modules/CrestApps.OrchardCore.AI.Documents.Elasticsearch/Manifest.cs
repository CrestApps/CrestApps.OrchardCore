using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
Name = "AI Documents indexing using Elasticsearch",
    Description = "Provides services to index AI Documents in Elasticsearch indexes.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Artificial Intelligence",
    Dependencies =
    [
        ChatInteractionsConstants.Feature.ChatDocuments,
        "OrchardCore.Search.Elasticsearch",
    ]
)]
