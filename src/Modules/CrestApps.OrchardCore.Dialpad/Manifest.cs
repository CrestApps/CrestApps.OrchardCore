using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
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
    Description = "Provides the Dialpad telephony provider and its settings.",
    Category = "Telephony",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = DialpadConstants.Feature.ContactCenterVoice,
    Name = "Dialpad Contact Center Voice",
    Description = "Enables the Dialpad provider to place outbound contact center calls and handle their real-time call events.",
    Category = "Contact Center",
    Dependencies =
    [
        DialpadConstants.Feature.Area,
        ContactCenterConstants.Feature.Voice,
    ]
)]
