using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Id = AIConstants.Feature.OrchardCoreAIAgent,
    Name = "Orchard Core AI Agent",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Use natural language to run tasks, manage content, interact with OrchardCore features, and do much more with integrated AI-powered tools.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Area,
        "CrestApps.OrchardCore.Recipes",
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.OrchardCoreAIAgentBrowserAutomation,
    Name = "AI Agent Browser Automation",
    Description = "Provides optional Playwright-powered browser automation tools for the AI Agent so tenants can enable browser control separately from the core AI Agent tools.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]
