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

[assembly: Feature(
    Id = WorkforceConstants.Features.TimeOff,
    Name = "Workforce Time Off",
    Description = "Time-off types and policies with accrual, a balance ledger, requests and approvals, allotments that approve on their own with a waitlist, and blackout dates. Approved time off goes on the team calendar.",
    Category = "Workforce",
    Dependencies =
    [
        WorkforceConstants.Features.Workforce,
    ]
)]

[assembly: Feature(
    Id = WorkforceConstants.Features.LaborRules,
    Name = "Workforce Labor Rules",
    Description = "A catalog of labor rules combined into rule sets for teams and workers, each set to warn, block or need an override; schedules are checked before they are published, claims and trades as they are made, and late changes record predictability pay.",
    Category = "Workforce",
    Dependencies =
    [
        WorkforceConstants.Features.ShiftPlanning,
    ]
)]
