using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Outreach;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Outreach",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = OutreachConstants.Feature,
    Name = "Outreach",
    Description = "The one bulk-send engine for broadcasts and campaigns: a run sends to an audience in batches, skipping recipients who opted out or did not consent, holding text messages in quiet hours, and retrying failures. Messaging broadcasts and Marketing campaigns send through it.",
    Category = "Communication",
    EnabledByDependencyOnly = true
)]
