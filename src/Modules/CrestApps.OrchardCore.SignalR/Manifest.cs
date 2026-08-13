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
    Name = "SignalR (Deprecated)",
    Description = "This feature has been migrated to the Orchard Core Framework and should no longer be used. Instead, use the 'OrchardCore.SignalR' feature.",
    Category = "Communication",
    Dependencies =
    [
        "OrchardCore.SignalR",
    ]
)]

[assembly: Feature(
    Id = SignalRConstants.Feature.RedisBackplane,
    Name = "SignalR Redis Backplane (Deprecated)",
    Description = "This feature has been migrated to the Orchard Core Framework and should no longer be used. Instead, use the 'OrchardCore.SignalR.Redis' feature.",
    Category = "Communication",
    Dependencies =
    [
        SignalRConstants.Feature.Area,
        "OrchardCore.Redis",
        "OrchardCore.SignalR.Redis",
    ]
)]

[assembly: Feature(
    Id = SignalRConstants.Feature.AzureBackplane,
    Name = "SignalR Azure Backplane (Deprecated)",
    Description = "This feature has been migrated to the Orchard Core Framework and should no longer be used. Instead, use the 'OrchardCore.SignalR.Azure' feature.",
    Category = "Communication",
    Dependencies =
    [
        SignalRConstants.Feature.Area,
        "OrchardCore.SignalR.Azure",
    ]
)]
