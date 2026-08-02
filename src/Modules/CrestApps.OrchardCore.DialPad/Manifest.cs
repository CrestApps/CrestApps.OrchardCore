using CrestApps.OrchardCore;
using CrestApps.OrchardCore.DialPad;
using CrestApps.OrchardCore.Telephony;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "DialPad",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Integrates the DialPad telephony platform with the Telephony soft phone.",
    Category = "Telephony"
)]

[assembly: Feature(
    Id = DialPadConstants.Feature.Area,
    Name = "DialPad",
    Description = "Provides the DialPad telephony provider and its settings.",
    Category = "Telephony",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
    ]
)]
