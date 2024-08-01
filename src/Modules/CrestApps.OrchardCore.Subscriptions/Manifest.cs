using CrestApps.OrchardCore.Subscriptions.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Subscriptions",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Subscriptions",
    Id = SubscriptionsConstants.Features.ModuleId,
    Description = "Provides a way to process and manage subscriptions.",
    Category = "Subscriptions"
)]
