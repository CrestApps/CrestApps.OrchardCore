using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Calendars;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Calendars",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = CalendarsConstants.Features.Calendars,
    Name = "Calendars",
    Description = "A shared calendar: calendars and events with time zones, repeats and attendees, a day, week, month and agenda view, free/busy time and sharing. Other features and modules add calendar types.",
    Category = "Calendar",
    Dependencies =
    [
        "OrchardCore.Users",
        "OrchardCore.Roles",
        "CrestApps.OrchardCore.Resources",
    ]
)]

[assembly: Feature(
    Id = CalendarsConstants.Features.Teams,
    Name = "Team Calendars",
    Description = "Calendars that belong to a team (a role), with meetings, on-call duty and time out of office, and a page that shows when the team's members are free.",
    Category = "Calendar",
    Dependencies = [CalendarsConstants.Features.Calendars]
)]

[assembly: Feature(
    Id = CalendarsConstants.Features.Resources,
    Name = "Resource Calendars",
    Description = "Calendars for meeting rooms and equipment that only one group can use at a time, and that can be invited to events on any calendar.",
    Category = "Calendar",
    Dependencies = [CalendarsConstants.Features.Calendars]
)]


[assembly: Feature(
    Id = CalendarsConstants.Features.Office,
    Name = "Office Calendars",
    Description = "Calendars for an office: who is in, desk and room bookings up to the number of desks, and days the office is closed.",
    Category = "Calendar",
    Dependencies = [CalendarsConstants.Features.Calendars]
)]

[assembly: Feature(
    Id = CalendarsConstants.Features.Reminders,
    Name = "Calendar Reminders",
    Description = "Runs once a minute to raise the 'starting soon' and 'completed' moments of events, exactly once each, for workflows and other modules.",
    Category = "Calendar",
    Dependencies = [CalendarsConstants.Features.Calendars]
)]

[assembly: Feature(
    Id = CalendarsConstants.Features.Invitations,
    Name = "Calendar Invitations",
    Description = "Emails attendees an invitation with a calendar file when they are invited, and tells them when the event changes or is cancelled. Outside attendees answer from a link in the email.",
    Category = "Calendar",
    Dependencies =
    [
        CalendarsConstants.Features.Calendars,
        "OrchardCore.Email",
    ]
)]

[assembly: Feature(
    Id = CalendarsConstants.Features.Feeds,
    Name = "Calendar Feeds",
    Description = "Publishes a calendar as a private iCalendar address that Google Calendar, Outlook and phones can subscribe to, and imports and exports .ics files.",
    Category = "Calendar",
    Dependencies = [CalendarsConstants.Features.Calendars]
)]

[assembly: Feature(
    Id = CalendarsConstants.Features.External,
    Name = "External Calendars",
    Description = "Keeps a calendar in step with an external calendar through a provider. Includes a provider that subscribes to an iCalendar address.",
    Category = "Calendar",
    Dependencies = [CalendarsConstants.Features.Calendars]
)]

[assembly: Feature(
    Id = CalendarsConstants.Features.Api,
    Name = "Calendars API",
    Description = "HTTP endpoints to read calendars, events and free/busy time, and to create, change and cancel events, under /api/calendars/v1.",
    Category = "Calendar",
    Dependencies = [CalendarsConstants.Features.Calendars]
)]

