using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Dialpad;
using CrestApps.OrchardCore.Telephony;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Dialpad",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Integrates the Dialpad telephony platform with the Telephony soft phone.",
    Category = "Telephony"
)]

[assembly: Feature(
    Id = DialpadConstants.Feature.Area,
    Name = "Dialpad",
    Description = "Provides the Dialpad telephony provider and its settings. When Contact Center Voice is also enabled, the Dialpad provider automatically participates in contact center call orchestration.",
    Category = "Telephony",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
    ]
)]
