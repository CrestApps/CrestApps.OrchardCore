using CrestApps.OrchardCore;
using CrestApps.OrchardCore.SignalR.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "SignalR",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Provides all services needed to use SignalR functionality.",
    Category = "Communication"
)]

[assembly: Feature(
    Id = SignalRConstants.Feature.Area,
    Name = "SignalR",
    Description = "Provides SignalR hub routing, JSON protocol configuration, and client resources.",
    Category = "Communication"
)]

[assembly: Feature(
    Id = SignalRConstants.Feature.RedisBackplane,
    Name = "SignalR Redis Backplane",
    Description = "Routes SignalR messages across application nodes through a tenant-qualified Redis backplane.",
    Category = "Communication",
    Dependencies =
    [
        SignalRConstants.Feature.Area,
        "OrchardCore.Redis",
    ]
)]
