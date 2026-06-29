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
    Category = "Communications"
)]

[assembly: Feature(
    Id = DialPadConstants.Feature.Area,
    Name = "DialPad",
    Description = "Provides the DialPad telephony provider and its settings.",
    Category = "Communications",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = DialPadConstants.Feature.Dialer,
    Name = "DialPad Dialer",
    Description = "Implements the dialer-agnostic Contact Center dialer provider over DialPad so outbound campaigns dial through DialPad.",
    Category = "Communications",
    Dependencies =
    [
        DialPadConstants.Feature.Area,
        "CrestApps.OrchardCore.ContactCenter.Dialer",
    ]
)]
