using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Artificial Intelligence",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AIConstants.Feature.Area,
    Name = "Artificial Intelligence",
    Description = "Provides AI services.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        "OrchardCore.Markdown",
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.Deployments,
    Name = "AI Deployments",
    Description = "Manages AI model deployments.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.Chat,
    Name = "AI Chat",
    Description = "Manages AI chat profiles for various models.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        "OrchardCore.Liquid",
        "CrestApps.OrchardCore.Resources",
        AIConstants.Feature.Area,
    ]
)]
