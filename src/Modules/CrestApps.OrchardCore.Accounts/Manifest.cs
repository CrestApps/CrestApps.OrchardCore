using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Accounts;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Accounts",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AccountsConstants.Feature,
    Name = "Accounts",
    Description = "Keeps the businesses and households that contacts, orders, assets and work orders belong to as Account content items, with a picker, a search and an account page that other modules add to.",
    Category = "Content Management",
    Dependencies =
    [
        "CrestApps.OrchardCore.Resources",
        "OrchardCore.Contents",
        "OrchardCore.ContentTypes",
        "OrchardCore.Lists",
        "OrchardCore.Title",
    ]
)]
