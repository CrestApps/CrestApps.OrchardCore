using CrestApps.OrchardCore;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Subscriptions Core",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Subscriptions Core",
    Id = "CrestApps.OrchardCore.Subscriptions.Core",
    Description = "Core services for subscriptions module.",
    Category = "Subscriptions"
)]
