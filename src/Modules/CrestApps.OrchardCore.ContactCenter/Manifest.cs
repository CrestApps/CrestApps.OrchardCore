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
