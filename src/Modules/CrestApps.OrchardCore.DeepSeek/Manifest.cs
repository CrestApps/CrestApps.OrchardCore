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
    Name = "DeepSeek AI Services",
    Description = "Provides core AI services for DeepSeek technology.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        AIConstants.Feature.Area,
        "OrchardCore.Markdown",
    ]
)]

[assembly: Feature(
    Id = DeepSeekConstants.Feature.Chat,
    Name = "DeepSeek AI Services Chat",
    Description = "Manages AI chat profiles for DeepSeek providers.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        AIConstants.Feature.Chat,
        DeepSeekConstants.Feature.Area,
        "OrchardCore.Liquid",
        "CrestApps.OrchardCore.Resources",
    ]
)]

[assembly: Feature(
    Id = DeepSeekConstants.Feature.CloudChat,
    Name = "DeepSeek Cloud AI Chat",
    Description = "Manages AI chat profiles for DeepSeek Cloud services.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        DeepSeekConstants.Feature.Chat,
        AIConstants.Feature.Deployments,
    ]
)]
