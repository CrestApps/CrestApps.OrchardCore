using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Telnyx",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Integrates the Telnyx voice platform with the Telephony soft phone.",
    Category = "Telephony"
)]

[assembly: Feature(
    Id = TelnyxConstants.Feature.Area,
    Name = "Telnyx",
    Description = "Provides the Telnyx telephony provider, its browser WebRTC soft phone, and signed call-event webhooks.",
    Category = "Telephony",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = TelnyxConstants.Feature.ContactCenterVoice,
    Name = "Telnyx Contact Center Voice",
    Description = "Enables the Telnyx provider to place outbound contact center calls, bridge live calls to agents, and handle their real-time call events.",
    Category = "Contact Center",
    Dependencies =
    [
        TelnyxConstants.Feature.Area,
        ContactCenterConstants.Feature.Voice,
    ]
)]
