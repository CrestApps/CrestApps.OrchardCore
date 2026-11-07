using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Seo;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Advanced SEO",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = StructuredDataConstants.Feature.Area,
    Name = "Advanced SEO",
    Description = "Helps search engines and AI agents understand the site. Publishes schema.org structured data (JSON-LD) for the site and the content types you add the Schema.org part to, with an editor that suggests the properties of each type, and the data other modules add, such as product offers.",
    Category = "Content Management",
    Dependencies =
    [
        OrchardCoreFeatureIds.Contents,
        OrchardCoreFeatureIds.Liquid,
    ]
)]
