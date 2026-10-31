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

[assembly: Feature(
    Id = WorkforceConstants.Features.ShiftPlanning,
    Name = "Workforce Shift Planning",
    Description = "Shift templates with breaks, rotations, coverage requirements, and team schedules planned as drafts and published to the team calendar, with a log of every change after publishing.",
    Category = "Workforce",
    Dependencies =
    [
        WorkforceConstants.Features.Workforce,
    ]
)]
