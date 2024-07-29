using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Subscriptions",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Subscriptions",
    Id = "OrchardCore.CrestApps.Subscriptions",
    Description = "Provides a way to process and manage subscriptions.",
    Category = "Subscriptions"
)]
