# CrestApps.OrchardCore.Scheduling

Appointment booking on top of the shared calendar (`CrestApps.OrchardCore.Calendars`). Staff, rooms and equipment have
weekly working hours, date overrides or shifts read from a calendar's `Shift` events; appointment types set the length, start-time
interval, buffers, notice, horizon, daily limit, group size and how resources are picked. Open times are working hours
less busy calendar time, and `IAppointmentService` books under a distributed lock per resource with a concurrency check,
so a resource can never be booked twice. Every appointment is an event on the resource's **Appointments** calendar.

## Features

- **Scheduling** (`CrestApps.OrchardCore.Scheduling`): appointment types, resources, appointments, holds, the waitlist,
  the admin screens under **Calendars**, and, with `OrchardCore.Workflows`, the appointment workflow events and tasks.
- **Scheduling Self-Service** (`.SelfService`): public booking pages at `/book/{slug}` and manage links.
- **Scheduling API** (`.Api`): `/api/scheduling/v1`.

With `CrestApps.OrchardCore.AI` on, Scheduling also registers customer-facing AI tools that find open times and book,
move and cancel appointments (a startup with `[RequireFeatures("CrestApps.OrchardCore.AI")]`, no feature of its own).

## Dependencies

- Scheduling: `CrestApps.OrchardCore.Calendars`.
- The AI tools: `CrestApps.OrchardCore.AI`.
- The Appointment Starting Soon workflow event: `CrestApps.OrchardCore.Calendars.Reminders`.

Full documentation: <https://orchardcore.crestapps.com/docs/modules/scheduling>
