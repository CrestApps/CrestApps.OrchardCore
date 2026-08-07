using CrestApps.OrchardCore;
using CrestApps.OrchardCore.SignalR.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "SignalR Azure Backplane",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Routes SignalR messages across application nodes through the Azure SignalR Service.",
    Category = "Communication",
    Dependencies =
    [
        SignalRConstants.Feature.Area,
    ]
)]
