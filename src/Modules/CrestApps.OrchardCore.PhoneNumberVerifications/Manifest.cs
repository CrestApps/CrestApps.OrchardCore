using CrestApps.OrchardCore;
using CrestApps.OrchardCore.PhoneNumberVerifications;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Phone Number Verifications",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Communication"
)]

[assembly: Feature(
    Name = "Phone Number Verifications",
    Id = PhoneNumberVerificationsConstants.Features.Area,
    Category = "Communication",
    EnabledByDependencyOnly = true,
    Description = "Provides a provider-agnostic framework for verifying phone numbers, storing results on contact content items, reporting, and background revalidation.",
    Dependencies =
    [
        PhoneNumbersConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Name = "AbstractAPI Phone Number Verification",
    Id = PhoneNumberVerificationsConstants.Features.AbstractApi,
    Category = "Communication",
    Description = "Verifies phone numbers using the AbstractAPI Phone Validation service.",
    Dependencies =
    [
        PhoneNumberVerificationsConstants.Features.Area,
    ]
)]
