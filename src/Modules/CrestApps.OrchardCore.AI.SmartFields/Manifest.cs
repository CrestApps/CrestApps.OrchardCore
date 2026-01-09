using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Smart Fields",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = "CrestApps.OrchardCore.AI.SmartFields",
    Name = "AI Smart Fields",
    Description = "Provides AI-enhanced editors for TextField with autocomplete functionality powered by AI profiles.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.ChatCore,
        AIConstants.Feature.ChatApi,
    ]
)]
