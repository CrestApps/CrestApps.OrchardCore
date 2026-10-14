using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Products.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Product Search (Elasticsearch)",
    Description = "Lets product listings search an Elasticsearch index: keyword relevance, filter counts, ranges and sorts for large catalogs.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Content Management",
    Dependencies =
    [
        ProductConstants.Feature.Listing,
        "OrchardCore.Elasticsearch",
    ]
)]
