using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI File Sources",
    Description = "Adds a File AI data source populated by file sources. A file source reads a folder on a schedule and stores each file's text, figures, charts and tables as separately retrievable knowledge.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Artificial Intelligence - Knowledgebase",
    Dependencies =
    [
        AIConstants.Feature.DataSources,

        // Ingestion is document processing: the pipeline, the readers and the knowledge store all come
        // from this feature, and the run service cannot be constructed without them. Declaring it also
        // orders its registrations ahead of ours, which is what lets the HTML reader this feature's own
        // registration adds win the keyed ".html" slot over the plain-text one.
        ChatInteractionsConstants.Feature.ChatDocuments,
    ]
)]
