using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Users.Core;
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
    Description = "Provides the core infrastructure and services for the contact center: the interaction lifecycle and history log, the durable domain-event log, baseline permissions, settings, and the administration menu. Enable this first, then add the capabilities you need.",
    Category = "Contact Center",
    Dependencies =
    [
        OmnichannelConstants.Features.Activities,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.AgentServices,
    Name = "Contact Center Agent Services",
    Description = "Provides only the shared agent-profile directory services (profile store, manager, index, and storage collection) that resolve an operator's agent identity. It carries no administration screens. Enabled automatically as a dependency of the Agents feature and of any module that reuses agent identity, such as the SMS Workspace.",
    Category = "Contact Center",
    EnabledByDependencyOnly = true
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Agents,
    Name = "Contact Center Agents",
    Description = "Adds agent profiles, skills, queue/campaign sign-in, and the durable agent availability, presence, heartbeat, and after-call recovery that track who is working, together with their administration screens.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Area,
        ContactCenterConstants.Feature.AgentServices,
        UsersConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.AgentEntitlements,
    Name = "Contact Center Agent Entitlements",
    Description = "Restricts which queues and campaigns each agent may sign in to, with an administration screen to grant that access. When this feature is disabled, any agent may sign in to any queue or campaign with no per-agent setup.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Agents,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.BusinessHours,
    Name = "Contact Center Business Hours",
    Description = "Adds business-hours calendars, their administration, and the evaluation service used to gate work distribution and automated Omnichannel sends. Enabled on its own or pulled in by any feature that needs it.",
    Category = "Contact Center"
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Queues,
    Name = "Contact Center Work Distribution",
    Description = "Adds work queues, queue items, reservations, and the policy-based routing strategies and activity assignment that distribute work to available agents, together with their administration screens.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Agents,
        ContactCenterConstants.Feature.BusinessHours,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Dialer,
    Name = "Contact Center Outbound Dialer",
    Description = "Adds outbound calling over CRM activities: dialing profiles and their administration, mandatory compliance screening, callbacks, and Manual or Preview activity batches placed through the contact center's voice provider.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Voice,
        ContactCenterConstants.Feature.Queues,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.DialerPaced,
    Name = "Contact Center Paced Dialing",
    Description = "Adds Power and Progressive paced dialing that automatically dials for available agents, layering scheduled pacing on top of the Outbound Dialer, which already provides mandatory compliance screening and the dialing-profile administration.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Dialer,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Voice,
    Name = "Contact Center Voice",
    Description = "Routes inbound and outbound voice calls through the Voice Contact Center Call Router while Telephony providers execute media operations.",
    Category = "Contact Center",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        ContactCenterConstants.Feature.Queues,
        ContactCenterConstants.Feature.RealTime,
        ContactCenterConstants.Feature.RecordingCore,
        TelephonyConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.InboundVoice,
    Name = "Contact Center Inbound Voice",
    Description = "Adds inbound voice front doors that map dialed numbers to queues, qualify callers, apply business-hours decisions, set priority, and handle closed-hours calls.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Voice,
        ContactCenterConstants.Feature.Queues,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.VoiceMedia,
    Name = "Contact Center Voice Media",
    Description = "Adds executable bidirectional media-provider resolution for active voice calls.",
    Category = "Contact Center",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        ContactCenterConstants.Feature.Voice,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.RecordingCore,
    Name = "Contact Center Recording Governance",
    Description = "Provides the shared recording-access governance and audit services used by both call recording and voicemail playback. Enabled automatically by the capabilities that need it.",
    Category = "Contact Center",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        ContactCenterConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Recording,
    Name = "Contact Center Call Recording",
    Description = "Adds provider-gated call-recording orchestration, recording-state events, and recording settings for voice interactions.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Voice,
        ContactCenterConstants.Feature.RecordingCore,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.SecureCapture,
    Name = "Contact Center Secure Data Capture",
    Description = "Adds agent-assisted secure data capture: an agent sends a live customer to a dedicated secure page to enter sensitive data (such as a card number), which is tokenized at submission so the agent, the supervisor, and the recording never see the raw value.",
    Category = "Contact Center",
    Dependencies =
    [
        ContactCenterConstants.Feature.Recording,
        TelephonyConstants.Feature.SoftPhoneCore,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Supervision,
    Name = "Contact Center Supervision & Live Dashboard",
    Description = "Adds the real-time supervisor dashboard with live queue and agent monitoring, plus provider-gated monitor, whisper, and barge actions.",
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
    Description = "Adds the shared SignalR hub and real-time presence, offer, and queue broadcasts consumed by the agent desktop, supervision, and soft-phone experiences. Enabled automatically as a dependency of those capabilities.",
    Category = "Contact Center",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        ContactCenterConstants.Feature.Queues,
        "OrchardCore.SignalR",
    ]
)]
