using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Data Sources - Elasticsearch",
    Description = "Adds Elasticsearch support for AI data source document embeddings, vector search, and indexing.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.DataSources,
        "OrchardCore.Search.Elasticsearch",
    ]
)]
