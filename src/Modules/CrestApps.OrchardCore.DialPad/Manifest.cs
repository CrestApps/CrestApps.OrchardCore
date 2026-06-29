using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.DialPad;
using CrestApps.OrchardCore.Telephony;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "DialPad",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Integrates the DialPad telephony platform with the Telephony soft phone.",
    Category = "Communication"
)]

[assembly: Feature(
    Id = DialPadConstants.Feature.Area,
    Name = "DialPad",
    Description = "Provides the DialPad telephony provider and its settings.",
    Category = "Communication",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = DialPadConstants.Feature.Dialer,
    Name = "DialPad Contact Center Voice",
    Description = "Implements the Contact Center voice provider boundary over DialPad so the Voice Contact Center Call Router can place outbound calls through DialPad.",
    Category = "Communication",
    Dependencies =
    [
        DialPadConstants.Feature.Area,
        ContactCenterConstants.Feature.Voice,
    ]
)]
