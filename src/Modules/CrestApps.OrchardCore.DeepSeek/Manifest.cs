using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "DeepSeek AI Chat",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = "CrestApps.OrchardCore.DeepSeek",
    Name = "DeepSeek AI Chat",
    Description = "Provides a way to interact with the DeepSeek service provider.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.Deployments,
    ]
)]
