using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "AI Documents - Azure Blob Storage",
    Description = "Stores uploaded AI documents in Azure Blob Storage using Orchard Core file storage abstractions.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Artificial Intelligence",
    Dependencies =
    [
        ChatInteractionsConstants.Feature.ChatDocuments,
    ]
)]
