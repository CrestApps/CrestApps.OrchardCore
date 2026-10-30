# CrestApps.OrchardCore.Calendars

A shared calendar foundation for Orchard Core. Calendars and events live in the `Calendars` YesSql collection; what a
calendar is for comes from its **calendar type**, which any feature registers in code with
`services.AddCalendarType(name, builder)` (`CrestApps.OrchardCore.Calendars.Abstractions`). Events keep their wall-clock
time and IANA time zone, repeat by RFC 5545 rules, have attendees (users, guests, rooms and equipment), and are checked
against a calendar's overlap limit under a distributed lock.

## Features

- **Calendars** (`CrestApps.OrchardCore.Calendars`): calendars, events, the General and Personal types, the calendar
  view (day, week, month, agenda), the calendar list and editors, permissions, the `Calendars` recipe and deployment
  step, and, with `OrchardCore.Workflows`, the calendar workflow events and tasks.
- **Team Calendars** (`.Teams`), **Resource Calendars** (`.Resources`), **Workforce Calendars** (`.Workforce`),
  **Office Calendars** (`.Office`): the Team, Room, Equipment, Workforce and Office types, and the team availability page.
- **Calendar Reminders** (`.Reminders`): "starting soon" and "completed" moments, raised once per occurrence.
- **Calendar Invitations** (`.Invitations`): invitation, update and cancellation emails with an `.ics` file.
- **Calendar Feeds** (`.Feeds`): private iCalendar feed addresses, `.ics` import and export.
- **External Calendars** (`.External`): subscriptions to external iCalendar addresses. Google Calendar and Microsoft 365
  providers are not available yet; only the `ICalendarProvider` seam and the iCalendar provider exist.
- **Calendars API** (`.Api`): `/api/calendars/v1`.
- **Calendar AI Tools** (`.AI`): AI tools that list calendars and events, find free time, and create, move and cancel
  events.

## Extension points

`AddCalendarType`, `ICalendarHandler`, `ICalendarEventHandler`, `ICalendarEventObserver` (after commit),
`ICalendarBusyTimeSource`, `ICalendarEventSource`, `ICalendarReminderOffsetProvider`, `ICalendarReminderObserver` and
`ICalendarProvider`. See the `crestapps-calendar-types` skill in `.agents/skills`.

## Dependencies

- Calendars: `OrchardCore.Users`, `OrchardCore.Roles`, `CrestApps.OrchardCore.Resources`.
- Calendar Invitations: `OrchardCore.Email`. Calendar AI Tools: `CrestApps.OrchardCore.AI`.

Full documentation: <https://orchardcore.crestapps.com/docs/modules/calendars>
