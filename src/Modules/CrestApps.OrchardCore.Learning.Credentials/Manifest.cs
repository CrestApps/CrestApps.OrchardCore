using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Credentials;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning Digital Credentials",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = CredentialsConstants.Features.DigitalCredentials,
    Name = "Digital Credentials",
    Description = "Open Badges 3.0 credentials (issuer) on the W3C Verifiable Credentials Data Model 2.0: credential definitions as content items, awarded when a course is completed, a quiz passed, a learning path completed or a certification issued, or by hand; each award is signed as a JSON Web Token with a key kept in Secrets, has public endpoints and a verification page, and can be revoked through a Bitstring Status List.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
        OrchardCoreFeatureIds.Secrets,
    ]
)]
