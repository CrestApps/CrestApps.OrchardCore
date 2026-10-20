using CrestApps.OrchardCore;
using OrchardCore.DisplayManagement.Manifest;

[assembly: Theme(
    Name = "Storefront",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "A theme for an online store: a header with the store's search, market, cart and account, breadcrumbs above each page, and a footer with how to reach the store. Built on TheTheme, so its pages and shapes carry over.",
    BaseTheme = "TheTheme",
    Tags = ["Commerce", "Storefront", "Bootstrap"]
)]
