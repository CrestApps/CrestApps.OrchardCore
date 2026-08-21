using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.WebSockets;
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
        WebSocketsConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = TelnyxConstants.Feature.Sms,
    Name = "Telnyx SMS",
    Description = "Adds the Telnyx SMS/MMS provider and its signed inbound and delivery-receipt messaging webhook, so Telnyx numbers can send and receive text messages through the SMS Communication Portal. Reuses the Telnyx account API key and webhook public key from the Telnyx provider settings.",
    Category = "Telephony",
    Dependencies =
    [
        TelnyxConstants.Feature.Area,
        "OrchardCore.Sms",
    ]
)]
