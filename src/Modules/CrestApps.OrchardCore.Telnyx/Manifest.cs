using CrestApps.OrchardCore;
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
    Description = "Provides the Telnyx telephony provider, its browser WebRTC soft phone, and signed call-event webhooks. When Contact Center Voice is also enabled, the Telnyx provider automatically participates in contact center call orchestration.",
    Category = "Telephony",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
    ]
)]
