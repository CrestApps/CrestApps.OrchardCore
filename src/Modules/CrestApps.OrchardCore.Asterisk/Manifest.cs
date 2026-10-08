using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Asterisk;
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
    Id = AsteriskConstants.Feature.Area,
    Name = "Asterisk",
    Description = "Provides the Asterisk telephony provider and its settings. When Contact Center Voice is also enabled, the Asterisk provider automatically participates in contact center call orchestration and, when Contact Center Voice Media is enabled, in bidirectional RTP media sessions.",
    Category = "Telephony",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
    ]
)]
