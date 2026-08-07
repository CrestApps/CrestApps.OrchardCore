using CrestApps.OrchardCore;
using CrestApps.OrchardCore.SignalR.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "SignalR Redis Backplane",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Routes SignalR messages across application nodes through a tenant-qualified Redis backplane.",
    Category = "Communication",
    Dependencies =
    [
        SignalRConstants.Feature.Area,
        "OrchardCore.Redis",
    ]
)]
