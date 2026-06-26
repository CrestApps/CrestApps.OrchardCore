using CrestApps.OrchardCore;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Phone Numbers Services",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Communications",
    Description = "Provides phone number parsing, validation, and E.164 formatting services."
)]

[assembly: Feature(
    Name = "Phone Numbers Services",
    Id = PhoneNumberVerificationsConstants.Features.PhoneNumbers,
    Category = "Communications",
    EnabledByDependencyOnly = true,
    Description = "Provides phone number parsing, validation, and E.164 formatting services."
)]
