using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Prompting;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Prompt Templates",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AIPromptingConstants.Feature.Area,
    Name = "AI Prompt Templates",
    Description = "Provides reusable AI prompt template management with feature-aware discovery, Liquid-based rendering, and prompt selection UI for AI Profiles and Chat Interactions.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
    ]
)]
