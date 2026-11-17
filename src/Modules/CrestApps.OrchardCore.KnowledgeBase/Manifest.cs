using CrestApps.OrchardCore;
using CrestApps.OrchardCore.KnowledgeBase;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Knowledge Base",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = KnowledgeBaseConstants.Feature,
    Name = "Knowledge Base",
    Description = "Help articles for everyone, for signed-in people or for staff only: a Knowledge Article content type with categories, drafts and versions, a help center at /help with search, \"was this helpful?\" feedback, and suggestions other modules show while people write a request or a reply.",
    Category = "Service Desk",
    Dependencies =
    [
        OrchardCoreFeatureIds.Contents,
        OrchardCoreFeatureIds.Title,
        OrchardCoreFeatureIds.Autoroute,
        OrchardCoreFeatureIds.Markdown,

        // An article's category is a term of the Knowledge Base Categories taxonomy.
        OrchardCoreFeatureIds.Taxonomies,

        // The anonymous "Was this helpful?" answers are limited through Orchard Core's Rate Limits.
        OrchardCoreFeatureIds.RateLimits,
    ]
)]
