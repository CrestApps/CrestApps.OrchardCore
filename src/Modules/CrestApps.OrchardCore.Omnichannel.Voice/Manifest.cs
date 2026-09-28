using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Voice;
using CrestApps.OrchardCore.Telephony;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Automated Voice",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Runs an automated AI voice conversation over any telephony provider.",
    Category = "Contact Center"
)]

[assembly: Feature(
    Id = OmnichannelVoiceConstants.Feature.Area,
    Name = "Automated Voice",
    Description = "Runs an automated voice conversation driven by an AI chat profile: the call is answered, the assistant speaks and listens through whichever telephony provider is carrying it, the caller can be escalated to a live agent, and the activity is settled with a summary and a disposition. A telephony provider supplies the audio; enable the provider's own automated voice feature alongside this one.",
    Category = "Contact Center",
    Dependencies =
    [
        TelephonyConstants.Feature.Area,
        AIConstants.Feature.Area,
        AIConstants.Feature.ChatCore,
        OmnichannelConstants.Features.Managements,
    ]
)]
