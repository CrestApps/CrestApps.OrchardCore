using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Data Sources - File Sources",
    Description = "Adds a File AI data source populated by file sources. A file source reads a folder on a schedule and stores each file's text, figures, charts and tables as separately retrievable knowledge.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Artificial Intelligence - Knowledgebase",
    Dependencies =
    [
        AIConstants.Feature.DataSources,
    ]
)]
