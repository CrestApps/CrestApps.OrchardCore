using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Omnichannel Messaging",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "A human-operated, channel-agnostic messaging workspace: one inbox and one conversation view for every non-voice channel (SMS today; email, WhatsApp, Messenger and others as further channel features), routed to agents and queues by the inbound entry points that answer each number, built on the Omnichannel addresses and the channel-neutral Contact Center features.",
    Category = "Contact Center"
)]

[assembly: Feature(
    Id = MessagingConstants.Feature.Workspace,
    Name = "Omnichannel Messaging Workspace",
    Description = "Adds the human messaging workspace: a customer-centric inbox where each customer's conversations on every enabled channel sit side by side, routing to agents and queues through inbound entry points, templates, broadcasts, and its own SignalR hub for real-time messaging. It carries no channel of its own — enable a channel feature such as SMS Messaging Channel to send and receive. Reuses only the shared Contact Center agent-profile services for operator identity (via the dependency-only Agent Services feature) and the inbound entry points, which say where each number's messages go.",
    Category = "Contact Center",
    Dependencies =
    [
        OmnichannelConstants.Features.ChannelEndpoints,
        ContactCenterConstants.Feature.AgentServices,
        ContactCenterConstants.Feature.EntryPoints,
        "OrchardCore.SignalR",
    ]
)]

[assembly: Feature(
    Id = MessagingConstants.Feature.RoutedDistribution,
    Name = "Omnichannel Messaging Routed Distribution",
    Description = "Push-assigns new department conversations, on every messaging channel, to the least-loaded available agent using Contact Center work distribution, and re-pools threads nobody picks up.",
    Category = "Contact Center",
    Dependencies =
    [
        MessagingConstants.Feature.Workspace,
        ContactCenterConstants.Feature.Queues,
    ]
)]
