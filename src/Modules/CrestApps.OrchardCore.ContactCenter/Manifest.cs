using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
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
        "CrestApps.OrchardCore.Omnichannel.Managements",
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Dialer,
    Name = "Contact Center Dialer",
    Description = "Adds dialer-agnostic outbound dialing profiles, pacing, and dialer activity batches over any installed dialer provider.",
    Category = "Communication",
    Dependencies =
    [
        ContactCenterConstants.Feature.Queues,
    ]
)]

[assembly: Feature(
    Id = ContactCenterConstants.Feature.Voice,
    Name = "Contact Center Voice",
    Description = "Routes inbound provider calls into queued CRM activities and offers them to available agents through the Telephony soft phone.",
    Category = "Communication",
    Dependencies =
    [
        ContactCenterConstants.Feature.Queues,
        "CrestApps.OrchardCore.Telephony.SoftPhone",
    ]
)]
