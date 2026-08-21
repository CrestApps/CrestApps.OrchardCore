using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Telephony.Sms;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "SMS Communication Portal",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "A human-operated two-way SMS portal: inbox, conversation threads, composer, and personal/department number routing, built on the Omnichannel number catalog and the channel-neutral Contact Center features.",
    Category = "Telephony"
)]

[assembly: Feature(
    Id = TelephonySmsConstants.Feature.Portal,
    Name = "SMS Communication Portal",
    Description = "Adds the human two-way SMS inbox and conversation workspace: DID-to-agent/queue number routing, the per-number provider dispatcher, two-way send/receive over the shared Omnichannel message store, and real-time messaging notifications. Reuses the channel-neutral Contact Center features (Agents, Work Distribution, Real-Time) without requiring Contact Center Voice.",
    Category = "Telephony",
    Dependencies =
    [
        OmnichannelConstants.Features.Managements,
        ContactCenterConstants.Feature.Queues,
        ContactCenterConstants.Feature.RealTime,
        "OrchardCore.Sms",
        "OrchardCore.SignalR",
    ]
)]
