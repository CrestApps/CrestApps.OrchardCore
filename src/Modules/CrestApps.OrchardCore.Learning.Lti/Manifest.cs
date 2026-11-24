using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Lti;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning External Tools",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = LtiConstants.Features.ExternalTools,
    Name = "External Tools",
    Description = "LTI 1.3 tools in courses, with the site as the platform: register a tool (an OpenID application plus its LTI settings and privacy level), add External tool course items or let course staff pick content with Deep Linking 2.0, launch them for learners with a signed id token (the platform key is kept in Secrets and rotated), and let tools read the roster (Names and Role Provisioning Services 2.0) and post grades (Assignment and Grade Services 2.0) through the OpenID server's token endpoint.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
        OrchardCoreFeatureIds.OpenIdServer,
        OrchardCoreFeatureIds.OpenIdValidation,
        OrchardCoreFeatureIds.Secrets,
    ]
)]
