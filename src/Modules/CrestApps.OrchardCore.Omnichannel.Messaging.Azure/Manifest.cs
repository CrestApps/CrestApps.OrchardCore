using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Omnichannel.Messaging;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Omnichannel Messaging - Azure Blob Storage",
    Description = "Stores the pictures exchanged in messaging conversations in Azure Blob Storage, so they survive redeployments, keeping the same encryption-at-rest as the local store.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Contact Center",
    Dependencies =
    [
        MessagingConstants.Feature.Workspace,
    ]
)]
