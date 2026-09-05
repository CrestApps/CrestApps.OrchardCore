using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "SMS Portal",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "A human-operated two-way SMS portal: inbox, conversation threads, composer, and personal/department number routing, built on the Omnichannel channel endpoints and the channel-neutral Contact Center features.",
    Category = "Contact Center"
)]

[assembly: Feature(
    Id = SmsPortalConstants.Feature.Portal,
    Name = "SMS Portal",
    Description = "Adds the human two-way SMS inbox and conversation portal: channel-endpoint routing to agents/queues, the per-number provider dispatcher, two-way send/receive over the shared Omnichannel message store, and its own SignalR hub for real-time messaging. Reuses only the shared Contact Center agent-profile services for operator identity (via the dependency-only Agent Services feature — not the Agents, Work Distribution, or Omnichannel Management administration) and the Omnichannel channel endpoints (via the dependency-only Channel Endpoints feature).",
    Category = "Contact Center",
    Dependencies =
    [
        OmnichannelConstants.Features.ChannelEndpoints,
        ContactCenterConstants.Feature.AgentServices,
        ContactCenterConstants.Feature.ProviderInbox,
        "OrchardCore.Sms",
        "OrchardCore.SignalR",
    ]
)]

[assembly: Feature(
    Id = SmsPortalConstants.Feature.RoutedDistribution,
    Name = "SMS Portal Routed Distribution",
    Description = "Push-assigns new department SMS conversations to the least-loaded available agent using Contact Center work distribution, and re-pools threads nobody picks up.",
    Category = "Contact Center",
    Dependencies =
    [
        SmsPortalConstants.Feature.Portal,
        ContactCenterConstants.Feature.Queues,
    ]
)]
