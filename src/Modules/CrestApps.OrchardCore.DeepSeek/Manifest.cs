using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.DeepSeek.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "DeepSeek AI Services",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = DeepSeekConstants.Feature.Area,
    Name = "DeepSeek AI Chat",
    Description = "Provides a way to interact with the DeepSeek service provider.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]
