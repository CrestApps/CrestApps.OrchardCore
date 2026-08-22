using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Sms.Workspace;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "SMS Workspace",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "A human-operated two-way SMS portal: inbox, conversation threads, composer, and personal/department number routing, built on the Omnichannel channel endpoints and the channel-neutral Contact Center features.",
    Category = "Communication"
)]

[assembly: Feature(
    Id = SmsWorkspaceConstants.Feature.Workspace,
    Name = "SMS Workspace",
    Description = "Adds the human two-way SMS inbox and conversation workspace: channel-endpoint routing to agents/queues, the per-number provider dispatcher, two-way send/receive over the shared Omnichannel message store, and its own SignalR hub for real-time messaging. Reuses Contact Center agents and work distribution for operator identity and queue membership (without Voice or the Contact Center real-time hub) and the Omnichannel channel endpoints (without the full Omnichannel management screens).",
    Category = "Communication",
    Dependencies =
    [
        OmnichannelConstants.Features.ChannelEndpoints,
        ContactCenterConstants.Feature.Queues,
        "OrchardCore.Sms",
        "OrchardCore.SignalR",
    ]
)]
