using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.SignalR.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Artificial Intelligence",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AIConstants.Feature.Chat,
    Name = "AI Chat",
    Description = "Provides UI to interact with AI models using the profiles.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        "OrchardCore.Liquid",
        "CrestApps.OrchardCore.Resources",
        AIConstants.Feature.ChatCore,
        SignalRConstants.Feature.Area,
        AIConstants.Feature.Area,
    ]
)]
