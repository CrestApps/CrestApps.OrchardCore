using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Telephony;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Telephony - Azure Blob Storage",
    Description = "Stores Contact Center call recordings in Azure Blob Storage, keeping the same encryption-at-rest as the local store.",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Telephony",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
    ]
)]
