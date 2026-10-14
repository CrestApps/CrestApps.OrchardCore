using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Products.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Product Search (Lucene)",
    Description = "Lets product listings search a Lucene index: keyword relevance, filter counts, ranges and sorts, kept on the server's disk.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Content Management",
    Dependencies =
    [
        ProductConstants.Feature.Listing,
        "OrchardCore.Lucene",
    ]
)]
