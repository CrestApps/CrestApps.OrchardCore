using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Telephony;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Contact Center",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Provides the contact center orchestration layer that turns the CRM into a full contact center.",
    Category = "Contact Center"
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Area,
    Name = "Contact Center",
    Description = "Provides the headless interaction lifecycle, durable domain event log, and baseline permissions.",
    Category = "Contact Center",
    Dependencies =
    [
        OmnichannelConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Admin,
    Name = "Contact Center Administration",
    Description = "Integrates Contact Center capabilities with the Omnichannel administration experience.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Area,
        OmnichannelConstants.Features.Managements,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Agents,
    Name = "Contact Center Agents",
    Description = "Adds agent profiles, presence, capacity, skills, and queue/campaign sign-in.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Queues,
    Name = "Contact Center Queues",
    Description = "Adds work queues, queue items, reservations, and availability-based activity assignment.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Admin,
        ContactCenterConstants.Feature.Availability,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Availability,
    Name = "Contact Center Availability",
    Description = "Adds agent presence, durable sessions, heartbeat state, and stale-session recovery without requiring real-time transport.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Agents,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Dialer,
    Name = "Contact Center Dialer",
    Description = "Adds outbound dialing profiles, callbacks, and Manual or Preview activity batches that route calls through Contact Center Voice providers.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Voice,
        ContactCenterConstants.Feature.Routing,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Compliance,
    Name = "Contact Center Outbound Compliance",
    Description = "Adds the mandatory outbound eligibility gate, suppression auditing, retry limits, and calling-window enforcement.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Dialer,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.DialerAutomated,
    Name = "Contact Center Automated Dialer",
    Description = "Adds compliance-gated Power and Progressive dialing strategies and scheduled pacing.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Dialer,
        ContactCenterConstants.Feature.Compliance,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Voice,
    Name = "Contact Center Voice",
    Description = "Routes inbound and outbound voice calls through the Voice Contact Center Call Router while Telephony providers execute media operations.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Routing,
        TelephonyConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.EntryPoints,
    Name = "Contact Center Entry Points",
    Description = "Adds inbound voice entry-point administration, qualification, business-hours decisions, and queue ingress.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Voice,
        ContactCenterConstants.Feature.Routing,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Recording,
    Name = "Contact Center Recording",
    Description = "Adds provider-capability-gated recording orchestration and recording-state events for voice interactions.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Voice,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Routing,
    Name = "Contact Center Routing",
    Description = "Adds policy-based routing strategies and activity assignment orchestration over Contact Center queues.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Queues,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.VoiceSoftPhone,
    Name = "Contact Center Voice - Soft Phone",
    Description = "Projects Contact Center voice state into the Telephony soft phone and real-time agent experience.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Voice,
        ContactCenterConstants.Feature.RealTime,
        TelephonyConstants.Feature.SoftPhone,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.AgentDesktop,
    Name = "Contact Center Agent Desktop",
    Description = "Adds the CRM-integrated real-time workspace where agents manage presence, offers, active interactions, and recent work.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Availability,
        ContactCenterConstants.Feature.RealTime,
        ContactCenterConstants.Feature.VoiceSoftPhone,
        "CrestApps.OrchardCore.Omnichannel.Managements",
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Supervision,
    Name = "Contact Center Supervision",
    Description = "Adds the real-time supervisor dashboard and provider-capability-gated monitoring actions.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.RealTime,
        ContactCenterConstants.Feature.Voice,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.RealTime,
    Name = "Contact Center Real-Time",
    Description = "Adds the shared SignalR hub and real-time presence, offer, and queue broadcasts consumed by optional user experiences.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Queues,
        ContactCenterConstants.Feature.Availability,
        "CrestApps.OrchardCore.SignalR",
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Analytics,
    Name = "Contact Center Reports & Analytics",
    Description = "Adds enterprise executive, interaction, queue/SLA, agent, transfer, recording, campaign, and subject reports to the admin Reports area.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Queues,
        ReportsConstants.Feature,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Workflows,
    Name = "Contact Center - Workflows",
    Description = "Adds a Contact Center domain-event activity and bridge for Orchard Core Workflows.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Area,
        "OrchardCore.Workflows",
    ]
)]
