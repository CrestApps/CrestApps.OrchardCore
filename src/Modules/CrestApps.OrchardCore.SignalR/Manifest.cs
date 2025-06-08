using CrestApps.OrchardCore;
using CrestApps.OrchardCore.SignalR.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "SignalR",
    Id = SignalRConstants.Feature.Area,
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Provides all services needed to use SignalR functionality.",
    Category = "Communication"
)]
