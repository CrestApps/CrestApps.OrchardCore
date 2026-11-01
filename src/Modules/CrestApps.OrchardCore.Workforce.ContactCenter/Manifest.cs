using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.ContactCenter;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Workforce for Contact Center",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = WorkforceContactCenterConstants.Features.ContactCenter,
    Name = "Workforce for Contact Center",
    Description = "Gives every contact center agent a worker profile, so agents are scheduled, take time off and are paid like every other worker.",
    Category = "Workforce",
    Dependencies =
    [
        WorkforceConstants.Features.Workforce,
        ContactCenterConstants.Feature.Agents,
    ]
)]

[assembly: Feature(
    Id = WorkforceContactCenterConstants.Features.SignInPunches,
    Name = "Contact Center Sign-in Punches",
    Description = "Clocks agents in when they sign in to the contact center and out when they sign out, unless they already clocked in another way.",
    Category = "Workforce",
    Dependencies =
    [
        WorkforceContactCenterConstants.Features.ContactCenter,
        WorkforceConstants.Features.TimeClock,
    ]
)]

[assembly: Feature(
    Id = WorkforceContactCenterConstants.Features.Adherence,
    Name = "Workforce Adherence",
    Description = "Compares agents' published schedules with their contact center states: a live board, alerts when an agent is out of adherence too long, daily adherence and conformance, exceptions and reports.",
    Category = "Workforce",
    Dependencies =
    [
        WorkforceContactCenterConstants.Features.ContactCenter,
        WorkforceConstants.Features.ShiftPlanning,
        ContactCenterConstants.Feature.IntervalStatistics,
    ]
)]
