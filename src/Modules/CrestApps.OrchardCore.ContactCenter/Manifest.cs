using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Contact Center",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Provides the contact center orchestration layer that turns the CRM into a full contact center.",
    Category = "Communication"
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Area,
    Name = "Contact Center",
    Description = "Provides the interaction lifecycle, the durable domain event log, baseline permissions, and admin navigation.",
    Category = "Communication",
    Dependencies =
    [
        OmnichannelConstants.Features.Managements,
        "OrchardCore.Users",
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Agents,
    Name = "Contact Center Agents",
    Description = "Adds agent profiles, presence, capacity, skills, and queue/campaign sign-in.",
    Category = "Communication",
    Dependencies =
    [
        ContactCenterConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Queues,
    Name = "Contact Center Queues",
    Description = "Adds work queues, queue items, reservations, and availability-based activity assignment.",
    Category = "Communication",
    Dependencies =
    [
        ContactCenterConstants.Feature.Agents,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Dialer,
    Name = "Contact Center Dialer",
    Description = "Adds outbound dialing profiles, pacing, and dialer activity batches that route calls through Contact Center Voice providers.",
    Category = "Communication",
    Dependencies =
    [
        ContactCenterConstants.Feature.Voice,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Voice,
    Name = "Contact Center Voice",
    Description = "Routes inbound and outbound voice calls through the Voice Contact Center Call Router while Telephony providers execute media operations.",
    Category = "Communication",
    Dependencies =
    [
        ContactCenterConstants.Feature.Queues,
        "CrestApps.OrchardCore.Telephony.SoftPhone",
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.RealTime,
    Name = "Contact Center Real-Time",
    Description = "Adds the SignalR hub, live agent sessions with heartbeat and stale-session cleanup, and real-time presence, offer, and queue broadcasts for the agent desktop and supervisor dashboards.",
    Category = "Communication",
    Dependencies =
    [
        ContactCenterConstants.Feature.Queues,
        "CrestApps.OrchardCore.SignalR",
    ]
)]
