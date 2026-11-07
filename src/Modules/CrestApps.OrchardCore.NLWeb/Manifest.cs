using CrestApps.OrchardCore;
using CrestApps.OrchardCore.NLWeb;
using CrestApps.OrchardCore.Seo;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "NLWeb",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = NLWebConstants.Feature.Area,
    Name = "NLWeb",
    Description = "Lets people and AI agents ask the site questions in natural language with the NLWeb protocol, and answers with the matching content items described as schema.org objects. Uses the site's search indexes, and, when AI is enabled, rewrites follow-up questions, ranks the results, writes summaries and answers, and gives MCP clients an ask tool.",
    Category = "Search",
    Dependencies =
    [
        StructuredDataConstants.Feature.Area,

        // The settings page lists the site's index profiles, which the Indexing feature stores.
        OrchardCoreFeatureIds.Indexing,
    ]
)]
