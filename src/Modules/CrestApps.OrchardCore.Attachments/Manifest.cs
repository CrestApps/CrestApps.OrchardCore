using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Attachments;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Attachments",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AttachmentsConstants.Features.Attachments,
    Name = "Attachments",
    Description = "Files attached to records, such as the files of a timeline note. Each file is checked by its own bytes, scanned by the site's file scanners, kept encrypted in the tenant's storage, and served only to people the record's module allows, or through a signed link.",
    Category = "Organization"
)]
