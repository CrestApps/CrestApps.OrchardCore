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
    Id = WorkforceContactCenterConstants.Features.Forecasting,
    Name = "Workforce Forecasting",
    Description = "Forecasts the work of planning groups of queues from their interval statistics, staffs every 15 minutes with Erlang C or Erlang A to a service goal, and turns the active forecast into the team's coverage for the auto-scheduler.",
    Category = "Workforce",
    Dependencies =
    [
        WorkforceContactCenterConstants.Features.ContactCenter,
        WorkforceConstants.Features.ShiftPlanning,
        ContactCenterConstants.Feature.IntervalStatistics,
    ]
)]

[assembly: Feature(
    Id = WorkforceContactCenterConstants.Features.Intraday,
    Name = "Workforce Intraday",
    Description = "The day as it happens for each planning group: forecast against actual work, agents needed against scheduled and available, a reforecast of the rest of the day, the quietest times for meetings, and voluntary overtime and time off that workers take in My Work.",
    Category = "Workforce",
    Dependencies =
    [
        WorkforceContactCenterConstants.Features.Forecasting,
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
