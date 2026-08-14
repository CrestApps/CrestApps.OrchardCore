using CrestApps.OrchardCore;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Phone Number Verifications",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Phone Verification"
)]

[assembly: Feature(
    Name = "Phone Number Verifications",
    Id = PhoneNumberVerificationsConstants.Features.Area,
    Category = "Phone Verification",
    EnabledByDependencyOnly = true,
    Description = "Provides a provider-agnostic framework for verifying phone numbers, storing results on contact content items, and background revalidation.",
    Dependencies =
    [
        "OrchardCore.Contents",
        PhoneNumberVerificationsConstants.Features.PhoneNumbers,
    ]
)]

[assembly: Feature(
    Name = "AbstractAPI Phone Number Verification",
    Id = PhoneNumberVerificationsConstants.Features.AbstractApi,
    Category = "Phone Verification",
    Description = "Verifies phone numbers using the AbstractAPI Phone Validation service.",
    Dependencies =
    [
        PhoneNumberVerificationsConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Name = "Veriphone Phone Number Verification",
    Id = PhoneNumberVerificationsConstants.Features.Veriphone,
    Category = "Phone Verification",
    Description = "Verifies phone numbers using the Veriphone phone number validation service.",
    Dependencies =
    [
        PhoneNumberVerificationsConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Name = "Twilio Phone Number Verification",
    Id = PhoneNumberVerificationsConstants.Features.Twilio,
    Category = "Phone Verification",
    Description = "Verifies phone numbers using the Twilio Lookup service.",
    Dependencies =
    [
        PhoneNumberVerificationsConstants.Features.Area,
    ]
)]
