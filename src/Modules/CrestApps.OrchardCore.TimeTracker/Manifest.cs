using CrestApps.OrchardCore;
using CrestApps.OrchardCore.TimeTracker;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Time Tracker",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = TimeTrackerConstants.Features.TimeTracker,
    Name = "Time Tracker",
    Description = "Track people's time in stages you set up, such as Work, Break and Lunch, totalled by categories such as Payable, Billable or Vacation: clock in, switch stage, clock out, review the day, and report the hours of each category.",
    Category = "Workforce",
    Dependencies =
    [
        "OrchardCore.Users",
        "CrestApps.OrchardCore.Users",
    ]
)]
