using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Resources;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Users;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Skills",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = SkillsConstants.Features.Skills,
    Name = "Skills",
    Description = "One skill catalog for the site: skills held at a proficiency from 1 to 5, certifications that expire, the certificates people hold and reminders before they run out. Contact Center routing and workforce scheduling read the same skills.",
    Category = "Workforce",
    Dependencies =
    [
        UsersConstants.Feature.Area,
        ResourcesConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = SkillsConstants.Features.IssuedCertificates,
    Name = "Skills: Issued Certificates",
    Description = "Prints the certificates the site issues from a certificate template, with a QR code, and proves them on a public verification page that shows a revocation at once.",
    Category = "Workforce",
    Dependencies =
    [
        SkillsConstants.Features.Skills,
        OrchardCoreFeatureIds.Contents,
        OrchardCoreFeatureIds.Title,
        OrchardCoreFeatureIds.Liquid,
        OrchardCoreFeatureIds.Media,
    ]
)]
