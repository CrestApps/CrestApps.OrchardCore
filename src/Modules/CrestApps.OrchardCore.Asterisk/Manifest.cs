using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Telephony;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Asterisk",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Integrates the Asterisk telephony platform with the Telephony soft phone.",
    Category = "Telephony"
)]

[assembly: Feature(
    Id = AsteriskConstants.Feature.ContactCenterVoice,
    Name = "Asterisk Contact Center Voice",
    Description = "Adapts Asterisk call execution and real-time call events to Contact Center voice orchestration.",
    Category = "Contact Center",
    Dependencies =
    [
        AsteriskConstants.Feature.Area,
        ContactCenterConstants.Feature.Voice,
    ]
)]

[assembly: Feature(
    Id = AsteriskConstants.Feature.ContactCenterMedia,
    Name = "Asterisk Contact Center Media",
    Description = "Adds bidirectional RTP media sessions for active Asterisk Contact Center calls.",
    Category = "Contact Center",
    Dependencies =
    [
        AsteriskConstants.Feature.ContactCenterVoice,
        ContactCenterConstants.Feature.VoiceMedia,
    ]
)]

[assembly: Feature(
    Id = AsteriskConstants.Feature.Area,
    Name = "Asterisk",
    Description = "Provides the Asterisk telephony provider and its settings.",
    Category = "Telephony",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
    ]
)]
