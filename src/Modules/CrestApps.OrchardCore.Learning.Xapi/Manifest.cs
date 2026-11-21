using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Xapi;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Learning Record Store",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = XapiConstants.Features.LearningRecordStore,
    Name = "Learning Record Store",
    Description = "An xAPI 1.0.3 Learning Record Store at /learning/xapi/: statements with validation, immutability, voiding, attachments and signed statements, the state, activity profile and agent profile documents, and the activities, agents and about resources. Clients are Orchard Core OpenID applications whose roles hold the LRS permissions (a bearer token or HTTP Basic), or launch tokens of launched content; the sign-in cookie never opens the LRS.",
    Category = "Learning",
    Dependencies =
    [
        LearningConstants.Features.Learning,
        OrchardCoreFeatureIds.OpenId,
        OrchardCoreFeatureIds.OpenIdManagement,
    ]
)]
