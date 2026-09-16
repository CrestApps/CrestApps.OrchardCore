using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Voice;
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
    Description = "Adds the Telnyx SMS/MMS provider and its signed inbound and delivery-receipt messaging webhook, so Telnyx numbers can send and receive text messages through the SMS Communication Portal. Configured from the OrchardCore_Sms_Telnyx appsettings section or the Telnyx SMS settings on the SMS settings screen.",
    Category = "Communication",
    Dependencies =
    [
        TelnyxConstants.Feature.Area,
        "OrchardCore.Sms",
    ]
)]

[assembly: Feature(
    Id = TelnyxConstants.Feature.AiVoice,
    Name = "Telnyx AI Voice Agent",
    Description = "Carries an automated voice conversation over Telnyx: the Phone omnichannel processor dials a contact, and Telnyx text-to-speech and real-time transcription supply the audio for the provider-neutral Automated Voice conversation.",
    Category = "Contact Center",
    Dependencies =
    [
        TelnyxConstants.Feature.Area,
        OmnichannelVoiceConstants.Feature.Area,
        AIConstants.Feature.Area,
        AIConstants.Feature.ChatCore,
        OmnichannelConstants.Features.Managements,
    ]
)]
