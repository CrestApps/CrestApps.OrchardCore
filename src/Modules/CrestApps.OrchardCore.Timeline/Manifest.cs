using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Timeline;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Timeline",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = TimelineConstants.Features.Timeline,
    Name = "Timeline",
    Description = "Comments, internal notes and events on any record, newest first, with who wrote them, the changes they record and their attachments. Modules show a record's timeline with a composer on their own pages, and the people mentioned in a note are notified.",
    Category = "Organization"
)]
