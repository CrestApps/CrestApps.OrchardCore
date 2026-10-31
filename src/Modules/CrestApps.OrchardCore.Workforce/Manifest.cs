using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Workforce;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Workforce",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = WorkforceConstants.Features.Workforce,
    Name = "Workforce",
    Description = "Workers with or without a user account, the team hierarchy with managers who see only their teams, activity codes, availability, skills and certificates, and a Workforce calendar for each team.",
    Category = "Workforce",
    Dependencies =
    [
        CalendarsConstants.Features.Calendars,
        SkillsConstants.Features.Skills,
    ]
)]
