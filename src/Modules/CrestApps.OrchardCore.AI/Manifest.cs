using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI",
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
    Name = "Artificial Intelligence Deployments",
    Description = "Provides a way to manage AI model Deployments.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.Chat,
    Name = "Artificial Intelligence Chat",
    Description = "Provides a way to manage AI chat profiles for AI models.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        "OrchardCore.Liquid",
        "CrestApps.OrchardCore.Resources",
        AIConstants.Feature.Area,
    ]
)]
