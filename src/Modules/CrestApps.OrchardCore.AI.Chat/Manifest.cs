using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.SignalR.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Artificial Intelligence",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AIConstants.Feature.Chat,
    Name = "AI Chat",
    Description = "Provides UI to interact with AI models using the profiles.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        "OrchardCore.Liquid",
        "CrestApps.OrchardCore.Resources",
        AIConstants.Feature.ChatCore,
        SignalRConstants.Feature.Area,
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.ChatAdminWidget,
    Name = "AI Chat Admin Widget",
    Description = "Provides a floating AI chat widget on every admin page, allowing users to interact with a predefined AI profile.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Chat,
        AIConstants.Feature.OrchardCoreAIAgent,
    ]
)]

[assembly: Feature(
    Id = AIConstants.Feature.ChatAnalytics,
    Name = "AI Chat Analytics",
    Description = "Tracks chat widget usage metrics (unique visitors, handle time, containment rate, abandonment rate) and provides reporting with extensible display drivers.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        AIConstants.Feature.Chat,
        AIConstants.Feature.ChatCore,
    ]
)]
