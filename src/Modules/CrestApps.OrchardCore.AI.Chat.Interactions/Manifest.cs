using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Chat Interactions",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ChatInteractionsConstants.Feature.ChatInteractions,
    Name = "AI Chat Interactions",
    Description = "Provides ad-hoc AI chat interactions with configurable parameters without predefined profiles.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        "OrchardCore.Liquid",
        "CrestApps.OrchardCore.Resources",
        AIConstants.Feature.ChatCore,
        "OrchardCore.SignalR",
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = ChatInteractionsConstants.Feature.ProfileBuilder,
    Name = "AI Profile Builder",
    Description = "Builds AI profiles by role-play: give the AI feedback on its replies and it rewrites its own instructions, then save the result as an AI profile.",
    Category = "Artificial Intelligence",
    Dependencies =
    [
        ChatInteractionsConstants.Feature.ChatInteractions,
    ]
)]
