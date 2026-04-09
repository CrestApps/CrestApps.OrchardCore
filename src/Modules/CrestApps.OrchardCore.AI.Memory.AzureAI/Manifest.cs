using CrestApps.Core.AI.Memory;
using CrestApps.OrchardCore;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Memory indexing using Azure AI Search",
    Description = "Provides services to index AI memory in Azure AI Search indexes.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Artificial Intelligence",
    Dependencies =
    [
    MemoryConstants.Feature.Memory,
    "OrchardCore.Indexing",
    "OrchardCore.Search.AzureAI",
    ]
    )]
