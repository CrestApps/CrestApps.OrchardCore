using CrestApps.OrchardCore;
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
    Name = "AI Services",
    Description = "Provides AI services.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        "CrestApps.OrchardCore.Resources",
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
    Id = AIConstants.Feature.DataSources,
    Name = "AI Data Source Management",
    Description = "Provides a way to manage AI data sources.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.ChatCore,
    Name = "AI Chat Services",
    Description = "Provides all the necessary services to enable chatting with AI models using profiles.",
    Category = "Artificial Intelligence",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        "OrchardCore.Liquid",
        "CrestApps.OrchardCore.Resources",
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.ChatApi,
    Name = "AI Chat WebAPI",
    Description = "Provides a RESTful API for interacting with the AI chat.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.ChatCore,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.ConnectionManagement,
    Name = "AI Connection Management",
    Description = "Provides user interface to manage AI connections.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.Deployments,
    ]
)]
