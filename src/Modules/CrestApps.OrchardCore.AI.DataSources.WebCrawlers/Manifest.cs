using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Data Sources - Web Crawlers",
    Description = "Adds a Web AI data source populated by strategy-based web crawlers that scrape public websites (starting with sitemap discovery) and index each page into the AI Knowledge Base for RAG.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Artificial Intelligence - Knowledgebase",
    Dependencies =
    [
        AIConstants.Feature.DataSources,
    ]
)]
