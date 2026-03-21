using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Playwright;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Artificial Intelligence",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = PlaywrightConstants.Feature.AdminWidget,
    Name = "AI Playwright Browser Automation",
    Description = "Enables the AI Chat Admin Widget agent to drive a real browser via Playwright. Admins can watch, stop, and close browser automation tasks directly from the chat widget.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.ChatAdminWidget,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]
