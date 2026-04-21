using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Data Sources",
    Description = "Provides AI data source management, knowledge base indexing, and RAG search capabilities.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]
