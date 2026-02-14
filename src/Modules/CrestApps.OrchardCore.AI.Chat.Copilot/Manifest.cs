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
    Id = "CrestApps.OrchardCore.AI.Chat.Copilot",
    Name = "AI Copilot Orchestrator",
    Description = "Provides a GitHub Copilot SDK-based orchestrator for AI chat sessions.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]
