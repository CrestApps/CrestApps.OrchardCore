using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Seo;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Structured Data",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = StructuredDataConstants.Feature.Area,
    Name = "Structured Data",
    Description = "Publishes schema.org structured data (JSON-LD) for the site and its content items, so search engines and AI agents understand them: the organization and web site, a schema.org type and property map for each content type, and the data other modules add, such as product offers.",
    Category = "Content Management",
    Dependencies =
    [
        "OrchardCore.Contents",
        "OrchardCore.Liquid",
    ]
)]
