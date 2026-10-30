using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Scheduling;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Scheduling",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = SchedulingConstants.Features.Scheduling,
    Name = "Scheduling",
    Description = "Appointment types, staff, rooms and equipment with working hours, and booking that never double-books a resource. Appointments go on the shared calendar.",
    Category = "Calendar",
    Dependencies = [CalendarsConstants.Features.Calendars]
)]

[assembly: Feature(
    Id = SchedulingConstants.Features.SelfService,
    Name = "Scheduling Self-Service",
    Description = "Public booking pages where customers pick a time and book, and a link in their confirmation to move or cancel.",
    Category = "Calendar",
    Dependencies = [SchedulingConstants.Features.Scheduling]
)]

[assembly: Feature(
    Id = SchedulingConstants.Features.Api,
    Name = "Scheduling API",
    Description = "HTTP endpoints to find open times and to book, move and cancel appointments, under /api/scheduling/v1.",
    Category = "Calendar",
    Dependencies = [SchedulingConstants.Features.Scheduling]
)]

[assembly: Feature(
    Id = SchedulingConstants.Features.AITools,
    Name = "Scheduling AI Tools",
    Description = "AI tools that find open times and book, move and cancel appointments for customers.",
    Category = "Calendar",
    Dependencies =
    [
        SchedulingConstants.Features.Scheduling,
        "CrestApps.OrchardCore.AI",
    ]
)]
