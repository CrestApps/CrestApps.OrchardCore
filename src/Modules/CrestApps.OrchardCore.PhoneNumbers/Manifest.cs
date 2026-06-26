using CrestApps.OrchardCore;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Phone Numbers Services",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Communication",
    Description = "Provides phone number parsing, validation, and E.164 formatting services."
)]

[assembly: Feature(
    Name = "Phone Numbers Services",
<<<<<<< HEAD
    Id = PhoneNumberVerificationsConstants.Features.PhoneNumbers,
    Category = "Communications",
=======
    Id = PhoneNumbersConstants.Features.Area,
    Category = "Communication",
>>>>>>> a5b8f877564bb7bd9f2c71143ae1ea23805214b1
    EnabledByDependencyOnly = true,
    Description = "Provides phone number parsing, validation, and E.164 formatting services."
)]
