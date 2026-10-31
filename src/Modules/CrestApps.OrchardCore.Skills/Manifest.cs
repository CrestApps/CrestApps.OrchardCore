using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Users.Core;
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
        "CrestApps.OrchardCore.Resources",
    ]
)]
