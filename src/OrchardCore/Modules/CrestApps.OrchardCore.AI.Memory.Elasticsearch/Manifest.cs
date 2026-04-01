using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Memory;
using OrchardCore.Modules.Manifest;
[assembly: Module(
    Name = "AI Memory indexing using Elasticsearch",
    Description = "Provides services to index AI memory in Elasticsearch indexes.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Artificial Intelligence",
    Dependencies =
    [
    MemoryConstants.Feature.Memory,
    "OrchardCore.Search.Elasticsearch",
    ]
    )]
