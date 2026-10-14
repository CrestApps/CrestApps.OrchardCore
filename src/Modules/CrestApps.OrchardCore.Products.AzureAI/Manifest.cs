using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Products.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Product Search (Azure AI Search)",
    Description = "Lets product listings search an Azure AI Search index: keyword relevance, filter counts, ranges and sorts, hosted by Azure.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Content Management",
    Dependencies =
    [
        ProductConstants.Feature.Listing,
        "OrchardCore.Indexing",
        "OrchardCore.AzureAI",
    ]
)]
