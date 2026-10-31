---
sidebar_label: Calendars
title: Calendars
description: "A shared calendar foundation for Orchard Core: calendar types registered in code, events with time zones, repeats and attendees, conflict limits for rooms and desks, free/busy, sharing, reminders, iCalendar feeds and subscriptions, workflows, an HTTP API and AI tools."
user_manual:
  - user-manual/calendars/calendars
---

The **Calendars** module is a calendar foundation that other modules build on. It stores calendars and their events, expands repeating events, works out free/busy time, enforces overlap limits for rooms, equipment and desks, and tells other features about changes once they are committed. What a calendar is for comes from its **calendar type**, which a feature registers in code: the module itself never switches on a type name. The admin screens are described in the [User Manual](../user-manual/calendars/calendars.md).

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.Calendars` |
| **Abstractions** | `CrestApps.OrchardCore.Calendars.Abstractions` (models, calendar type descriptors, services, extension interfaces) |
| **Core** | `CrestApps.OrchardCore.Calendars.Core` (stores, indexes, recurrence engine, conflict checking, iCalendar reader, writer and importer, external subscriptions) |
| **Category** | Calendar |
| **YesSql collection** | `Calendars` |

## Features

| Feature | ID | Depends on | Adds |
| --- | --- | --- | --- |
| Calendars | `CrestApps.OrchardCore.Calendars` | `OrchardCore.Users`, `OrchardCore.Roles`, `CrestApps.OrchardCore.Resources` | Calendars, events, the **General** and **Personal** types, the calendar view (day, week, month, agenda), the calendar list and editors, permissions, recipes and deployment |
| Team Calendars | `CrestApps.OrchardCore.Calendars.Teams` | Calendars | The **Team** type and the **Team Availability** page |
| Resource Calendars | `CrestApps.OrchardCore.Calendars.Resources` | Calendars | The **Room** and **Equipment** types, with location, seats and equipment fields; rooms and equipment can be invited to events |
| Office Calendars | `CrestApps.OrchardCore.Calendars.Office` | Calendars | The **Office** type: in office, desk bookings up to the desk count, visitors, office closed |
| Calendar Reminders | `CrestApps.OrchardCore.Calendars.Reminders` | Calendars | The **Remind** setting on events and the reminders that stay on people's admin pages until dismissed; a background task that raises "starting soon" and "completed" once per occurrence for workflows |
| Calendar Invitations | `CrestApps.OrchardCore.Calendars.Invitations` | Calendars, `OrchardCore.Email` | Invitation, update and cancellation emails with an `.ics` attachment, and answer links |
| Calendar Feeds | `CrestApps.OrchardCore.Calendars.Feeds` | Calendars | A private iCalendar feed address per calendar, and `.ics` import and export |
| External Calendars | `CrestApps.OrchardCore.Calendars.External` | Calendars | Subscriptions to external calendars, the sync background task and the built-in iCalendar address provider |
| Calendars API | `CrestApps.OrchardCore.Calendars.Api` | Calendars | The `/api/calendars/v1` endpoints |

Workflow activities need no feature of their own: they are registered when `OrchardCore.Workflows` is enabled. The [AI tools](#ai-tools) need none either: Calendars registers them when `CrestApps.OrchardCore.AI` is enabled. The **Starting Soon** and **Completed** workflow events also need Calendar Reminders. The `Calendars` recipe step needs `OrchardCore.Recipes.Core`, and the deployment step needs `OrchardCore.Deployment`.

:::note[Google Calendar and Microsoft 365]
External Calendars ships one read-only provider: a subscription to an iCalendar (`.ics`) address, which Google Calendar, Outlook and most calendar apps can publish. Its synchronization also supports providers that read only what changed and write changes back (two-way), so providers for Google Calendar and Microsoft 365 can be added as modules ([#1238](https://github.com/CrestApps/CrestApps.OrchardCore/issues/1238)). In the other direction, Calendar Feeds publishes a calendar as an address those apps can subscribe to.
:::

## Permissions

| Permission | Key | Default roles | Grants |
| --- | --- | --- | --- |
| Use calendars | `AccessCalendars` | Authenticated | Have a personal calendar, and reach the calendars one owns, is in the team role of, is shared with, or that are visible to everyone |
| Create calendars | `CreateCalendars` | Editor | Create calendars of the creatable types; the creator gets **Manage** on the new calendar |
| View all calendars | `ViewAllCalendars` | Administrator | **Read** on every calendar |
| Manage all calendars | `ManageAllCalendars` | Administrator | **Manage** on every calendar |
| List anyone's calendar events | `ListCalendarEvents` | Administrator | See anyone's schedule, or everyone's, in the calendar view (**Whose schedule**) |
| List own calendar events | `ListOwnCalendarEvents` | (with Use calendars) | See one's own schedule |
| View anyone's calendar events | `ViewCalendarEvents` | Administrator | See the details of any event instead of a busy block |
| View own calendar events | `ViewOwnCalendarEvents` | (with Use calendars) | See the details of events one created, attends, or that are on one's own calendar |

`ManageAllCalendars` implies the other three; `ViewAllCalendars` and `CreateCalendars` imply `AccessCalendars`. `ViewAllCalendars` implies `ListCalendarEvents` and `ViewCalendarEvents`, each of which implies its Own permission, and `AccessCalendars` implies both Own permissions.

### Categories

`CalendarCategory` (name, color, and an `OwnerId`, or none for a category everyone can use) lives in the Calendars collection behind `ICalendarCategoryService` (`ListAsync(userId)` gives the user's own and the shared ones). `CalendarEvent.CategoryId` puts an event in one. The calendar view colors an event with, in order, its own `Color`, its category's, its event type's, then its calendar's, and sends `categoryId` with each occurrence so the page can filter by category (busy blocks carry none). Users manage their own categories at **Calendars > Categories** (`AccessCalendars`); shared ones need `ManageAllCalendars`.

### Reminders on screen

`CalendarEvent.ReminderMinutes` holds how many minutes before the start an event reminds its people (one of `CalendarReminders.Choices`: 0, 5, 10, 15, 30, 60, 120 or 1440). New events that take time get `CalendarOptions.DefaultReminderMinutes` (10) unless their event type sets `RemindBefore(minutes)` (negative for none). `CalendarReminderService` raises each event's own offsets next to the ones workflows asked for, once each through the reminder log. `CalendarUserReminderObserver` turns them into a `CalendarUserReminder` per person (the owner of a user-owned calendar and every user attendee who hasn't declined). `CalendarRemindersFilter` adds the `CalendarReminders` shape and script to every full admin page of a user with `AccessCalendars`. The script polls `GET Admin/api/calendars/reminders` every minute and shows each reminder as a toast that doesn't hide by itself, until `POST Admin/api/calendars/reminders/{id}/dismiss` (with the antiforgery token header).

### Letting a user choose a time

Any page can let the user choose a time on their own calendar: render the `CalendarPicker` shape (`@await DisplayAsync(await New.CalendarPicker())`) and call `CrestAppsCalendarPicker.open({ date, durationMinutes, title })`. `date` is the time to show as chosen, as a wall-clock time in the user's time zone (what a date input in that zone holds). It shows the user's schedule in a modal and resolves with `{ start, end, local }` (`start` and `end` are instants; `local` is the wall-clock time in the user's time zone, which date inputs in that zone expect), or `null` when the user closes it.

The calendar view and the picker draw times in the user's time zone (`ILocalClock`), not the browser's, so a time clicked is the time a form gets. FullCalendar has no time zone support without a plugin, so it runs in UTC and is given dates whose UTC components are the wall-clock time in the user's zone; `Assets/js/shared/calendar-time.js` converts instants to those dates and back, and the page sends instants to the server. Omnichannel uses it for follow-up activities (see the Omnichannel activities docs).

### Schedules and event details

Code only checks the broad permission, with the resource, and `CalendarEventAuthorizationHandler` (an `AuthorizationHandler<PermissionRequirement>`) grants it through the Own permission when the resource is the user's:

```csharp
// May the user see this person's schedule? (CalendarSchedule.Everyone for everyone's.)
await authorizationService.AuthorizeAsync(User, CalendarPermissions.ListCalendarEvents, new CalendarSchedule(userId));

// May the user see this event's details, or only that the time is busy?
await authorizationService.AuthorizeAsync(User, CalendarPermissions.ViewCalendarEvents, calendarEvent);
```

- `ListCalendarEvents` with a `CalendarSchedule` succeeds through `ListOwnCalendarEvents` when the schedule is the user's own.
- `ViewCalendarEvents` with a `CalendarEvent` succeeds through `ViewOwnCalendarEvents` when the user created the event (`CreatedBy`), attends it, or owns its calendar. It also succeeds when the user can read the event's calendar (**Read**, or **Write** for a private event), so calendar sharing and the event permissions give the same answer.

The calendar view adds, changes, cancels and deletes events in a modal. The event pages (`calendar/events/choose`, `create`, `edit/{id}`, and the `cancel`, `delete`, `confirm` and `respond` posts) take `modal=true`: the editor then comes back as a partial view (`ChooseModal`, `CreateModal`, `EditModal`) that also writes out the scripts and styles its editor asked for, and a finished change answers `{ "succeeded": true|false, "message": "..." }` (`CalendarEditorOutcome`) instead of redirecting. Without `modal` they are the full admin pages, and a display driver added to the event editor shows up in both.

A person's schedule is the events on the calendars they own and the events they attend on any calendar; everyone's is every event. The calendar view's occurrences endpoint takes `schedule` (a user id or `everyone`) instead of `calendars`; it answers 403 without `ListCalendarEvents` for that schedule and returns each event the viewer can't `ViewCalendarEvents` as a busy block.

What a user can do with one calendar is a `CalendarAccessLevel`, worked out by `ICalendarAccessService` from the global permissions, ownership, sharing and visibility. Each level includes the ones below it:

| Level | Meaning |
| --- | --- |
| `None` | Nothing. |
| `FreeBusy` | See when events are busy, without titles or details. |
| `Read` | See events and their details. |
| `Write` | Add, change and cancel events. |
| `Manage` | Change the calendar itself and who it is shared with. |

The rules, in order: `ManageAllCalendars` gives **Manage**; `ViewAllCalendars` gives at least **Read**; without `AccessCalendars` nothing more applies. The owner of a user-owned calendar gets **Manage**, members of a team calendar's role get **Write**, each matching `CalendarShare` (a user or a role) adds its level, and the calendar's `Visibility` raises everyone signed in to **Read** (`Public`) or **FreeBusy** (`Shared`). `Private` adds nothing. A caller with **FreeBusy** only gets occurrences with `BusyOnly` set and no title or details.

## Calendar types

A calendar type is a `CalendarTypeDescriptor`, registered with `services.AddCalendarType(name, builder)` and kept in `CalendarOptions`. `ICalendarTypeManager` lists and finds the types and their event types. Calling `AddCalendarType` again with the same name extends the type, so one feature can add event types to a type another feature registered.

| Builder method | Descriptor member | Meaning |
| --- | --- | --- |
| `WithDisplayName`, `WithDescription`, `WithIcon`, `WithColor`, `WithPosition` | `DisplayName`, `Description`, `Icon`, `DefaultColor`, `Position` | How the type shows in the **Add calendar** menu and the calendar list. |
| `WithOwner(kind, onePerOwner)` | `OwnerKind`, `OnePerOwner` | Who a calendar of the type belongs to (`CalendarOwnerKind`): `None` (shared), `User` (owner id is a user id), `Role` (owner id is a role name) or `Resource` (the calendar is the resource). `OnePerOwner` allows one calendar per owner, as for **Personal**. |
| `WithCapabilities` | `Capabilities` | `CalendarCapabilities` flags: `Attendees`, `Recurrence`, `AllDay`, `FreeBusy`, `ExternalSync`, `Feed` (default `All`). |
| `WithVisibility` | `DefaultVisibility` | `Private`, `Shared` or `Public` for new calendars of the type. |
| `LimitConcurrent(capacity)` | `DefaultConflictPolicy`, `DefaultCapacity` | New calendars refuse a busy event when `capacity` busy events already overlap it. Without it, the policy is `AllowOverlaps`. |
| `ManagedByCode()` | `Creatable = false` | Keeps the type out of the **Add calendar** menu; only code creates calendars of it. |
| `AddEventType(name, displayName, configure)` | `EventTypes` | The kinds of events the calendars hold. A type with none gets the default `Event` type. |

Event type builder (`CalendarEventTypeBuilder`):

| Method | Effect |
| --- | --- |
| `Color(css)`, `Icon(classes)` | How events of the type are drawn. Without a color they use the calendar's. |
| `Busy()` / `Free()` | The default transparency. `Free()` also keeps the events out of the capacity count. |
| `AllDayByDefault()` | New events last whole days. |
| `Lasts(minutes)` | Default length (60). |
| `NeedsApproval()` | New events are created `Tentative` and someone with **Manage** confirms them. |
| `RequiresAttendee()` | An event must name at least one attendee, such as the person on call. |
| `IgnoresCapacity()` | Busy events of the type don't count toward the calendar's capacity. |

The built-in types:

| Type | Registered by | Owner | Conflict policy | Event types |
| --- | --- | --- | --- | --- |
| `General` | Calendars | None | Allow overlaps | Event, Meeting, Holiday (all day, free), Reminder (15 min, free) |
| `Personal` | Calendars | User, one per user; visibility `Shared` | Allow overlaps | Event, Meeting, Focus time, Out of office (all day) |
| `Team` | Team Calendars | Role | Allow overlaps | Meeting, On call (attendee, 8 h), Out of office (all day, attendee, ignores capacity), Milestone (all day, free) |
| `Room` | Resource Calendars | Resource; visibility `Public` | Limit 1 | Booking, Maintenance |
| `Equipment` | Resource Calendars | Resource; visibility `Public` | Limit 1 | Reservation, Maintenance |
| `Office` | Office Calendars | None; visibility `Public` | Limit 20 (the desk count, changed per calendar) | In office (all day, attendee), Desk booking (attendee, 4 h), Visitor, Office closed (all day, free) |
| `Appointments` | [Scheduling](./scheduling.md) | None, managed by code | Allow overlaps (Scheduling checks under its own lock) | Appointment, Blocked time |
| `Bookings` | [Bookings](./bookings.md#bookings-on-the-calendar), with Calendars on | None, managed by code | Allow overlaps | Booking |

Calendars has no workforce type (shifts, time off, breaks). A workforce management module is expected to register one with `AddCalendarType` (see [#1225](https://github.com/CrestApps/CrestApps.OrchardCore/issues/1225)), using `RequiresAttendee()` for the people on a shift and `NeedsApproval()` for time off.

A personal calendar is created for a user the first time they open **Calendars > Calendar** (`ICalendarService.GetOrCreatePersonalCalendarAsync`).

### Register a calendar type from another module

Reference `CrestApps.OrchardCore.Calendars.Abstractions`, depend on the `CrestApps.OrchardCore.Calendars` feature, and register the type in the feature's startup:

```csharp
using OrchardCore.Localization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCalendarType("Vehicles", type => type
            .WithDisplayName(LocalizationSource.Create<Startup>("Vehicle"))
            .WithDescription(LocalizationSource.Create<Startup>("A pool vehicle that one driver uses at a time."))
            .WithIcon("fa-solid fa-car")
            .WithOwner(CalendarOwnerKind.Resource)
            .WithVisibility(CalendarVisibility.Public)
            .LimitConcurrent(1)
            .AddEventType("Trip", LocalizationSource.Create<Startup>("Trip"), e => e.Color("#1971c2").RequiresAttendee().Lasts(240))
            .AddEventType("Service", LocalizationSource.Create<Startup>("Service"), e => e.Color("#868e96").AllDayByDefault()));
    }
}
```

For a type the module creates calendars of itself (like the Bookings and Appointments calendars), add `.ManagedByCode()`, create the calendar with `ICalendarService.NewAsync(type)` + `CreateAsync`, and set its `Source` and `SourceId` so `ICalendarService.FindBySourceAsync(source, sourceId)` finds it again.

Type-specific data goes in the `Properties` bag of `Calendar` or `CalendarEvent` (both are `CatalogItem`s: `Put<T>`, `GetOrCreate<T>`, `Alter<T>`), edited by a `DisplayDriver<Calendar>` or `DisplayDriver<CalendarEvent>` that returns `null` for other types. The editors are driver-built, so the fields appear on the right tab without the Calendars module knowing about them. The Room and Office settings (`ResourceCalendarSettings`, `OfficeCalendarSettings`) are built this way.

## Model

### Calendar

`Calendar` (`CatalogItem`): `Name`, `Description`, `Type`, `Color`, `TimeZoneId` (IANA), `OwnerKind`, `OwnerId`, `Visibility`, `Shares` (`CalendarShare`: `PrincipalKind` `User` or `Role`, `PrincipalId`, `DisplayName`, `Access`), `ConflictPolicy`, `Capacity`, `BookingVersion`, `Enabled`, `Source`, `SourceId`, `FeedToken`, and created/modified stamps. A disabled calendar keeps its events and takes no new ones.

### Event

`CalendarEvent` (`CatalogItem`):

| Member | Meaning |
| --- | --- |
| `CalendarId`, `EventType`, `Title`, `Description`, `Location`, `Color` | What and where. |
| `AllDay`, `Start`, `End`, `TimeZoneId` | The **wall-clock** start and end in an IANA zone. All-day events store dates with an exclusive end. |
| `StartUtc`, `EndUtc` | Derived from the wall-clock times on every save; never set them by hand. |
| `Status` | `Confirmed`, `Tentative` or `Cancelled`. |
| `Transparency` | `Busy` or `Free`. |
| `Visibility` | `Default` (as the calendar), `Public` or `Private` (others see only busy time). |
| `Attendees` | `CalendarAttendee`: `UserId` or `Email` + `Name`, or `ResourceCalendarId`; `Role` (`Required`, `Optional`, `Organizer`, `Resource`); `Response` (`NeedsAction`, `Accepted`, `Tentative`, `Declined`); `RespondedUtc`; `ResponseToken`. `GetKey()` returns `user:{id}`, `email:{address}` or `resource:{calendarId}`. |
| `Recurrence` | `CalendarRecurrence`: `Rule` (an RFC 5545 `RRULE`), `ExceptionDates` (wall-clock), `EndUtc` (null when it never ends). |
| `SeriesId`, `OriginalStartUtc` | Set on an override of one occurrence of a repeating event. |
| `Source`, `SourceId`, `Editable` | The module and record that own the event. Events with `Editable = false` are read-only in the calendar UI and changed only by their module through `ICalendarEventManager`. |
| `Uid`, `Sequence` | The iCalendar UID (defaults to the item id) and the revision number written to `.ics` files. |
| `ProviderMappings` | `CalendarProviderMapping` entries for external providers. |

### Recurrence model

- Times are wall-clock + zone. A weekly 09:00 meeting stays at 09:00 across daylight-saving changes, because the UTC instants are recomputed from the wall-clock time on save and on expansion.
- `RecurrenceRule` parses and formats the RRULE subset calendars need: `FREQ` (`DAILY`, `WEEKLY`, `MONTHLY`, `YEARLY`), `INTERVAL`, `COUNT`, `UNTIL`, `BYDAY` (with ordinals such as `2TU` or `-1FR` for monthly and yearly rules), `BYMONTHDAY`, `BYMONTH`, `BYSETPOS` and `WKST`. Occurrences start at the event's own time of day, so `BYHOUR`, `BYMINUTE` and `BYSECOND` are refused.
- `RecurrenceExpander` expands in local time with NodaTime (`DateTimeZoneProviders.Tzdb`). A wall-clock time that doesn't exist on a spring-forward day is skipped; one that repeats on a fall-back day takes the earlier instant, as RFC 5545 asks.
- A change to one occurrence is an **override**: a separate `CalendarEvent` with `SeriesId` (the master) and `OriginalStartUtc`. Cancelling one occurrence is an override with status `Cancelled`. **This and following** splits the series: the master gets an `UNTIL`, and a new master starts at the occurrence. This is the model Google Calendar and Microsoft 365 use, so a future two-way sync maps one-to-one.
- Wall-clock fields carry `WallClockDateTimeJsonConverter` (and `WallClockDateTimeListJsonConverter` for `ExceptionDates`), which write `yyyy-MM-ddTHH:mm:ss` without an offset and read back an unspecified kind. Without it, YesSql would treat an unspecified `DateTime` as server-local time and shift it.

### Storage and queries

| Index | Rows |
| --- | --- |
| `CalendarIndex` | One per calendar: name, type, owner kind and id, visibility, source and source id, feed token, enabled. |
| `CalendarShareIndex` | One per sharing entry: calendar, user or role, access level. Finds the calendars shared with a user without loading them all. |
| `CalendarEventIndex` | One per event: calendar, event type, status, transparency, `StartUtc`, `EndUtc`, `RangeEndUtc` (the end for a single event, the recurrence end or max for a series), recurring, series, original start, source and source id, UID. |
| `CalendarEventAttendeeIndex` | One per attendee: event, calendar, user, resource calendar, email, response, status, start and range end. "Everything a user attends" and resource bookings are found without loading every event. |
| `CalendarReminderLogIndex` | One per raised reminder (Calendar Reminders): its key and when it was raised. |

`ICalendarQueryService.GetOccurrencesAsync(CalendarOccurrenceQuery)` loads events with `StartUtc < to` and `RangeEndUtc > from`, expands masters, applies overrides and exception dates, adds the occurrences every `ICalendarEventSource` contributes when `IncludeEventSources` is set (the calendar view sets it), and returns `CalendarOccurrence`s ordered by start. The query can also read a user's invitations on any calendar (`AttendeeUserIds`), a resource's bookings on other calendars (`ResourceCalendarIds`), filter by event type or source, and, with a `Viewer`, reduce occurrences on calendars the viewer can't read to busy blocks. Queries are bounded by `CalendarOptions`: `MaxOccurrencesPerQuery` (5,000), `MaxEventsPerQuery` (5,000) and `MaxQueryDays` (400).

## Services

| Interface | What it does |
| --- | --- |
| `ICalendarTypeManager` | Lists and finds calendar types and event types. |
| `ICalendarService` | Creates (`NewAsync` + `CreateAsync`), updates, deletes and finds calendars (by id, ids or source), pages and lists them, and gets or creates a user's personal calendar. Runs the `ICalendarHandler`s. |
| `ICalendarEventManager` | Creates (`NewAsync` applies the event type's defaults), updates, cancels and deletes events; changes or cancels one occurrence, this and following, or the whole series (`UpdateOccurrenceAsync`, `CancelOccurrenceAsync` with `CalendarEditScope`); adds and removes attendees and records answers; finds events by id, source or UID; and `ValidateAsync` checks an event without saving it. |
| `ICalendarQueryService` | Occurrences in a range, and one occurrence of an event. |
| `ICalendarAvailabilityService` | `GetBusyAsync` (busy intervals for calendars, users and resources, unmerged so each says where it comes from, including every `ICalendarBusyTimeSource`) and `FindFreeWindowsAsync` (windows of a given length when everyone in the query is free). |
| `ICalendarAccessService` | The access level of a user on a calendar, the calendars a user reaches at a level, and whether a user may create calendars of a type. |

Every write takes a `CalendarChangeOptions`: `Actor` (a user id, `workflow`, or a module name), `Reason`, `SkipConflictCheck` (for a module that checked availability itself under its own lock), `Notify` (tell the after-commit observers, default true) and `SendInvitations` (default true). Writes return `CalendarResult<T>` with `Succeeded` and `Errors` (`CalendarError`, with `CalendarError.ConflictCode` for overlap refusals).

## Conflicts and concurrency

A calendar with `ConflictPolicy = LimitConcurrent` refuses a busy event when its `Capacity` of busy events already overlap at any moment. What counts: busy, non-cancelled events on the calendar whose event type counts toward capacity, plus events on other calendars that invite the calendar as a resource attendee and haven't been declined. For a repeating event, every occurrence in the next 366 days (up to 500) is checked.

On every create, change and move, `DefaultCalendarEventManager`:

1. Finds the calendars the event counts toward: its own calendar when it limits overlaps, and each invited room or equipment calendar.
2. Takes a distributed lock `CALENDAR_{calendarId}` on each, in id order so two requests never deadlock. A lock that can't be taken fails the write with a conflict error ("The calendar ... is busy. Try again in a moment.").
3. Checks the overlap with `CalendarConflictChecker`.
4. Raises each target calendar's `BookingVersion` and saves it with YesSql's concurrency check, so if another node changed the same calendar meanwhile, this commit fails instead of double-booking.
5. Stages the event.

## Extension points

| Contract | Use |
| --- | --- |
| `AddCalendarType(name, builder)` | Register or extend a calendar type and its event types. |
| `ICalendarHandler` (`CalendarHandlerBase`) | Validate (`ValidatingAsync` adds errors) and react to calendars being created, updated and deleted, inside the request. |
| `ICalendarEventHandler` (`CalendarEventHandlerBase`) | `ValidatingAsync` (add errors to refuse a save, such as a type's own rules), `SavingAsync` (adjust the event before it's staged), `SavedAsync`, `DeletingAsync`. Runs inside the request, before the session commits. |
| `ICalendarEventObserver` | `OnChangedAsync(CalendarEventChange)` runs **after the session commits**, in a new shell scope (a deferred task), so it never acts on a change that was rolled back. The change carries `Kind` (`Created`, `Updated`, `Cancelled`, `Deleted`, `AttendeeAdded`, `AttendeeRemoved`, `ResponseChanged`, `OccurrenceChanged`), `Event`, `Previous`, `Calendar`, `OccurrenceStartUtc`, `Attendee`, `Actor`, `Reason` and `SendInvitations`. Workflows and invitations listen here. An observer that throws is logged and the others still run. |
| `ICalendarBusyTimeSource` | Contribute busy time that isn't stored as events (a module's own reservations), so free/busy and Scheduling see it. Register with `services.AddCalendarBusyTimeSource<T>()`. |
| `ICalendarEventSource` | Contribute read-only occurrences to the calendar view from another module's records without copying them. Register with `services.AddCalendarEventSource<T>()`. Return events with `Editable = false`. |
| `ICalendarReminderOffsetProvider` | Tell Calendar Reminders how long before an occurrence its "starting soon" moment is raised. Each offset is raised once per occurrence. |
| `ICalendarReminderObserver` | `OnStartingSoonAsync` and `OnCompletedAsync`, raised exactly once per occurrence and offset. |
| `ICalendarProvider` (Core, `External`) | An external calendar provider: `Name`, `DisplayName`, `PullAsync(calendar, subscription)` returning `ICalendarEventData`, and `UsesAddress` / `IsConfigured` for providers connected through an account. Register it with `services.AddCalendarProvider<T>()`. |
| `IIncrementalCalendarProvider` (Core, `External`) | A provider that reads only what changed since a sync token: `PullChangesAsync` and `GetSyncTokenAsync`. |
| `IWritableCalendarProvider` (Core, `External`) | A provider that writes local changes back for a two-way subscription: `PushAsync` and `DeleteAsync`, with `CalendarProviderConflictException` and `CalendarProviderAuthorizationException`. |
| `DisplayDriver<Calendar>` / `DisplayDriver<CalendarEvent>` | Add type-specific fields to the editors. |

See the `crestapps-calendar-types` agent skill (`.agents/skills/crestapps-calendar-types/SKILL.md`) for worked examples and pitfalls.

## Reminders

Calendar Reminders runs `CalendarReminderBackgroundTask` every minute. It asks every `ICalendarReminderOffsetProvider` for offsets, finds occurrences that start within an offset or have just ended, and records each moment in a reminder log (`CalendarReminderLog`) under a key made of its kind, the event, the occurrence's start and the offset, so a moment fires once even after a restart or on two nodes. A run also looks back a little, so a minute missed while the site was down is still raised. Moving an occurrence changes its key, so its old reminders can never fire. The workflow integration registers an offset for each **Calendar Event Starting Soon** activity's **Minutes before the start**.

## Invitations

With Calendar Invitations on, `CalendarInvitationObserver` emails each attendee that has an email address (not the organizer, and not someone who declined) once a change is committed:

- **Invitation:** when an attendee is added, with an `.ics` `REQUEST`.
- **Updated:** when the title, time or place changes.
- **Cancelled:** when the event is cancelled or deleted, with an `.ics` `CANCEL`.

`CalendarInvitationTokenHandler` gives each email attendee a secret `ResponseToken`. The email's **Accept**, **Maybe** and **Decline** links go to `calendars/respond/{eventId}/{token}/{response}`, an anonymous endpoint that records the answer. Changes made with `SendInvitations = false` (recipes, imports) send nothing.

## iCalendar feeds, import and export

Calendar Feeds adds:

- **Feed:** `GET /calendars/feed/{token}.ics`, anonymous, the calendar's events as `text/calendar`. The token is `Calendar.FeedToken`, made, replaced or removed on the calendar's **Subscription address** page. Private events are written as busy time only.
- **Export:** `calendars/{id}/export` in the admin downloads the calendar as an `.ics` file.
- **Import:** `calendars/{id}/import` reads an `.ics` file with `ICalendarReader` (VEVENTs with `RRULE`, `EXDATE`, `RECURRENCE-ID` and `TZID`, up to 10,000 events) and `ICalendarImporter` matches events by UID, so importing the same file again updates instead of duplicating. A `RECURRENCE-ID` becomes an override of its series.

`ICalendarWriter` writes calendars and events as RFC 5545 with the product id `-//CrestApps//Orchard Core Calendars//EN`.

## External calendars

External Calendars keeps a calendar in step with an external calendar. The subscription is a `CalendarSubscription` in the calendar's properties: `Provider` (`ics` by default), `Url`, `IntervalMinutes` (60 by default, 5 to 10,080), `Enabled`, `TwoWay`, `ConflictPolicy`, and what synchronization keeps for itself: `SyncToken`, `PendingPush`, `PendingDeletes`, `NeedsReconnect`, `LastSyncedUtc`, `LastAttemptUtc`, `LastError`, `Failures` and `LastResult`.

`CalendarSyncBackgroundTask` runs every five minutes. It asks `CalendarSyncService.ListDueAsync` which calendars are due and synchronizes each one in its own scope, so a calendar whose provider fails, or whose save conflicts, never stops the others. A calendar is due when its interval has passed since the last attempt, or at the next run when it has local changes to write back.

One run:

1. **Reads** the external calendar. A provider that implements `IIncrementalCalendarProvider` reads only what changed since the subscription's `SyncToken`, and the run reads everything again when the token has expired. A full read removes the events that are gone from the external calendar.
2. **Imports** the events with `Source = External`, matched by the external event's id, and records on each event a `CalendarProviderMapping`: the calendar, the provider, the external id, the external copy's version and a fingerprint of the synchronized fields (title, description, location, times, time zone, status, busy or free, repeat rule, exceptions and attendee emails).
3. For a **two-way** subscription (only with a provider that implements `IWritableCalendarProvider`), imported events can be changed here, and the run **writes back** the events added or changed here (their fingerprint no longer matches) and deletes the external copies of events deleted here. Each write carries the version it last saw, so a change made externally in the meantime is caught.
4. **Records** the result on the subscription, shown on the calendar's **Subscription** card.

An event changed both here and in the external calendar since they were last synchronized is a **conflict**. The subscription's **When both copies changed** setting decides: **The external calendar's copy wins** (the default) overwrites the local change, and **This calendar's copy wins** keeps it and writes it over the external copy; an event deleted externally but changed here is then created again externally.

Failures never touch the events the calendar already has. A failure is recorded in `LastError` and the next try waits longer: the interval doubles with each failure in a row, up to 32 times the interval. When the external account refuses access (a revoked consent or an expired refresh token), the subscription is paused (`NeedsReconnect`) instead of retried, the card asks to reconnect, and saving the calendar again starts it. A health check named `calendars-external-sync` (tag `calendars`) reports **Degraded** while a subscription needs reconnecting or has failed three times in a row; it never reports the calendar unhealthy, since the calendar keeps working.

The built-in `ics` provider (`ICalendarUrlProvider`) is read-only. It accepts `https://`, `http://` and `webcal://` addresses, times out after 30 seconds, and refuses addresses that resolve to private or loopback networks.

To build another provider, such as Google Calendar or Microsoft 365, follow the `crestapps-calendar-provider` agent skill (`.agents/skills/crestapps-calendar-provider/SKILL.md`): the contracts, what the synchronization does for the provider, event mapping, connecting accounts, and a checklist.

## Workflows

With `OrchardCore.Workflows` enabled, the module adds activities in the **Calendar** category. Events are triggered by `CalendarWorkflowObserver` after the change commits, correlated by event id.

| Event | Raised when |
| --- | --- |
| Calendar Event Created (`CalendarEventCreatedEvent`) | An event is created. |
| Calendar Event Updated (`CalendarEventUpdatedEvent`) | An event, or one occurrence of a repeating event, changes. |
| Calendar Event Cancelled (`CalendarEventCancelledEvent`) | An event is cancelled. |
| Calendar Event Deleted (`CalendarEventDeletedEvent`) | An event is deleted. |
| Calendar Attendee Invited (`CalendarAttendeeInvitedEvent`) | An attendee is added. |
| Calendar Attendee Response Received (`CalendarAttendeeRespondedEvent`) | An attendee answers. |
| Calendar Event Starting Soon (`CalendarEventStartingSoonEvent`) | An occurrence starts within **Minutes before the start** (default 15). Needs Calendar Reminders. |
| Calendar Event Completed (`CalendarEventCompletedEvent`) | An occurrence ends. Needs Calendar Reminders. |

Every event has the same filters: `CalendarIds` (empty for every calendar), `CalendarTypes` (empty for every type), `EventTypes` (comma-separated technical names, empty for every kind) and `IgnoreWorkflowChanges` (default true), which skips changes whose actor is `workflow`, so a workflow that changes events doesn't start itself again.

Workflow input:

| Key | Value |
| --- | --- |
| `CalendarEvent` | The event's fields: `Id`, `CalendarId`, `EventType`, `Title`, `Description`, `Location`, `AllDay`, `Start`, `End` (wall-clock), `TimeZoneId`, `StartUtc`, `EndUtc`, `Status`, `Transparency`, `Recurrence`, `SeriesId`, `Source`, `SourceId`, `Attendees`. |
| `EventId`, `CalendarId`, `CalendarName`, `CalendarType`, `EventType`, `Title`, `StartUtc`, `EndUtc`, `Source`, `SourceId` | Shortcuts. |
| `Kind`, `Actor`, `Reason`, `OccurrenceStartUtc`, `Previous` | Change events: what happened, who did it, why, the occurrence changed, and the event as it was. |
| `Attendee`, `AttendeeEmail`, `AttendeeUserId`, `AttendeeResponse` | Attendee events. |
| `OccurrenceStartUtc`, `OccurrenceEndUtc`, `MinutesBefore` | Starting Soon and Completed. |

Times are ISO 8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`).

Tasks. Their text fields are Liquid. A time with an offset is a moment converted to the event's zone; one without an offset is a wall-clock time in the event's zone; a date (`yyyy-MM-dd`) makes an all-day event. Attendees are one per line or comma: an email, a name with the email in angle brackets, or `user:{userId}`. Each task can rename its output with **OutputName**.

| Task | Fields | Outcomes | Output |
| --- | --- | --- | --- |
| Create Calendar Event | `CalendarId`, `EventType`, `Title`, `Start`, `End`, `Location`, `Description`, `Attendees` | Done, Failed | `CalendarEvent` (also the last result) |
| Update Calendar Event | `EventId`, `OccurrenceStart` (change only that occurrence), `Title`, `Start`, `End`, `Location`, `Description` | Done, NotFound, Failed | `CalendarEvent` |
| Cancel Calendar Event | `EventId`, `OccurrenceStart`, `Reason` | Done, NotFound, Failed | `CalendarEvent` |
| Get Calendar Event | `EventId` | Found, NotFound | `CalendarEvent` |
| Find Calendar Events | `CalendarIds`, `From`, `To` | Found, None | `CalendarEvents` |
| Add Calendar Attendee | `EventId`, `Attendees` | Done, NotFound, Failed | |
| Remove Calendar Attendee | `EventId`, `Attendees` | Done, NotFound, Failed | |
| Find Free Time | `CalendarIds`, `UserIds`, `From`, `To`, `DurationMinutes` | Found, None | `FreeTimes` |

Task changes are made with the actor `workflow`.

## HTTP API

The Calendars API feature maps `/api/calendars/v1`, authenticated with Orchard Core's `Api` scheme and without antiforgery. Every endpoint checks the caller's access to each calendar it touches, with the same rules as the calendar screens. `GET calendars`, `GET events`, `free-busy` and `free-time` answer `403` to a caller without **Use calendars** (`AccessCalendars`).

| Method and route | Does | Access |
| --- | --- | --- |
| `GET calendars` | The calendars the caller reaches, with `id`, `name`, `type`, `color`, `timeZoneId`, `ownerId`, `visibility` and the caller's `access`. | FreeBusy |
| `GET events?calendarIds=&from=&to=` | Occurrences between two ISO 8601 times on the given calendars (all reachable ones when empty). Free/busy-only calendars return busy blocks. | FreeBusy |
| `GET events/{id}` | One event. | Read |
| `POST events` | Create an event from a `CalendarEventInput` (`calendarId`, `eventType`, `title`, `description`, `location`, `allDay`, `start`, `end`, `timeZoneId`, `status`, `transparency`, `recurrence` as an RRULE, `attendees`). Returns `201`. | Write |
| `PUT events/{id}?occurrence=&scope=` | Change an event, or with `occurrence` (the original start) and `scope` (`ThisOccurrence`, `ThisAndFollowing`, `AllOccurrences`) occurrences of a repeating event. | Write |
| `POST events/{id}/cancel?occurrence=&scope=` | Cancel an event or occurrences. | Write |
| `DELETE events/{id}` | Delete an event. Returns `204`. | Write |
| `POST events/{id}/respond` | Record the caller's answer: `{ "response": "Accepted" }` (or `Tentative`, `Declined`). | Invited |
| `POST free-busy` | Busy intervals for `calendarIds` and `userIds` (up to 50) between `from` and `to` (at most 62 days). | FreeBusy, and **Use calendars** |
| `POST free-time` | Windows of `durationMinutes` (5 to 1,440, default 30) when everyone is free, up to 50. | FreeBusy, and **Use calendars** |

An overlap refusal returns `409 Conflict` as Problem Details; other validation errors, including a missing body or a range whose end is not after its start, return `400` with the field errors. Cancelling an event that is already cancelled, or sending the same answer to an invitation again, succeeds without a change, so a retried request raises no second **Event Cancelled**, **Event Updated** or **Response Received** workflow event.

The admin calendar view uses its own endpoints under `Admin/api/calendars` (`occurrences`, `occurrences/move` and `users/search`), which are not a public API.

## AI tools

With `CrestApps.OrchardCore.AI` enabled, Calendars registers these tools in the **Calendar** category (an `AIToolsStartup` with `[RequireFeatures("CrestApps.OrchardCore.AI")]`). They act for the signed-in person using the AI, with that person's calendar access, and refuse anonymous callers and callers without **Use calendars** (`AccessCalendars`). Without that permission, `createCalendarEvent` doesn't create the person's personal calendar either.

| Function | Title | Description |
| --- | --- | --- |
| `listCalendars` | List calendars | Lists the calendars the user can see. |
| `findCalendarEvents` | Find calendar events | Lists the events on the user's calendars between two times (at most 62 days). |
| `findFreeTime` | Find free time | Finds times when the user and others (`userIds`, and calendars such as a room) are free (at most 31 days). |
| `createCalendarEvent` | Create calendar event | Adds an event to one of the user's calendars, or to their personal calendar when no calendar is named. |
| `moveCalendarEvent` | Move calendar event | Moves an event, or one occurrence of a repeating event, to another time. |
| `cancelCalendarEvent` | Cancel calendar event | Cancels an event, or one occurrence. |

See [AI Tools](../ai/tools.md#calendar-tools).

## Recipes and deployment

The **Calendars** deployment step exports every calendar (without its feed token and booking version), with **Include events** their events, and with **Include categories** (on by default) everyone's categories. The `Calendars` recipe step reads the same shape back: it creates or updates categories first, then calendars, matched by `itemId`, each with its events. A calendar needs a `type` whose feature is on. Recipe changes skip the overlap check and send no invitations.

Property names are camelCase and enums are written by name (a number is read too). Event `start` and `end` are wall-clock times in the event's `timeZoneId`, without an offset. The easiest way to get the exact shape is to export first.

```json
{
  "steps": [
    {
      "name": "Calendars",
      "Categories": [
        { "itemId": "holidays0000001", "name": "Company holiday", "color": "#e03131", "ownerId": null }
      ],
      "Calendars": [
        {
          "itemId": "companycalendar01",
          "name": "Company",
          "type": "General",
          "color": "#1971c2",
          "timeZoneId": "America/New_York",
          "visibility": "Public",
          "events": [
            {
              "itemId": "allhands0000001",
              "eventType": "Meeting",
              "title": "All hands",
              "start": "2026-11-02T09:00:00",
              "end": "2026-11-02T10:00:00",
              "timeZoneId": "America/New_York",
              "status": "Confirmed",
              "reminderMinutes": [ 10 ],
              "recurrence": { "rule": "FREQ=WEEKLY;BYDAY=MO" }
            }
          ]
        }
      ]
    }
  ]
}
```

With the **CrestApps Recipes** feature on, the module describes the `Calendars` recipe step (`CalendarsRecipeStepSchema`, an `IRecipeStep`) and the deployment step's settings (`CalendarsDeploymentStepSchema`) to it, from `RecipesSchemaStartup` (`[RequireFeatures("CrestApps.OrchardCore.Recipes")]`), so recipes can be checked and written with help.

## Report Builder data source

With **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) on, which the Report Builder needs, Calendars adds the **Calendars** data source (`Calendars`) to the Report Builder and to Orchard Core data pipelines. Its data sets need **Use calendars** (`AccessCalendars`) and follow the same rules as the calendar view, for the report's owner (the user whose principal runs the report):

- Only calendars the owner can **read** are reported: calendars they own, are in the team role of, are shared with at **Read** or above, or that are public, and every calendar with **View all calendars**. A calendar the owner can only see as free/busy time, and the events on it, are left out completely rather than reported as busy blocks.
- A private event on a calendar the owner can read but not change is reported with its time, type and status, but its **Title** and **Attendees** are empty and **Details hidden** is `true`, as it shows as busy time on the calendar. **View all calendars** reads every calendar but changes none, so private events stay hidden; **Manage all calendars** shows them.
- An event the owner is only invited to, on a calendar they can't read, is not reported.
- Descriptions, locations, attendees' names and email addresses, answer links, feed tokens and links to external calendars are never exposed.

Grouping and totals are left to the report engine, since a grouping statement in the database would read every calendar.

The **Calendars** data set (`Calendars`) has one row per calendar.

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Calendar ID | Identifies the calendar. |
| `Name` | Name | |
| `Type` | Type | Such as `General`, `Personal`, `Team`, `Room` or `Office`. |
| `OwnerKind` | Owner kind | `None`, `User`, `Role` or `Resource`. |
| `OwnerId` | Owner | The user ID, role name or resource it belongs to. |
| `OwnerUserId` | Owner user ID | The owner when a user owns it; references `Users`. |
| `Visibility` | Visibility | `Private`, `Shared` or `Public`. |
| `ShareCount` | Shared with | How many users and roles it is shared with. |
| `Enabled` | Enabled | |
| `TimeZoneId` | Time zone | |
| `ConflictPolicy` | Conflicts | What happens when events overlap. |
| `Capacity` | Capacity | How many events may overlap when the calendar limits them. |
| `Source` | Source | The module that owns it, such as `Scheduling`. |
| `CreatedUtc`, `ModifiedUtc` | Created, Last changed | |
| `CreatedBy` | Created by | References `Users`. |

The **Calendar events** data set (`CalendarEvents`) has one row per stored event: a one-time event, a repeating event (one row for the whole series, at its first occurrence), or a changed occurrence of one (with its `SeriesId`). Occurrences of a repeating event are not expanded into rows. Rows come newest first by **Starts**, the default date. Date ranges on it, joins on the event and calendar IDs, and filters on **Status** and **Busy or free** are applied in the database.

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Event ID | Identifies the event. |
| `EventType` | Event type | Such as `Event`, `Meeting`, `Shift` or `Appointment`. |
| `Title` | Title | Empty when the details are hidden. |
| `Status` | Status | `Confirmed`, `Tentative` or `Cancelled`. |
| `Visibility` | Visibility | `Default`, `Public` or `Private`. |
| `DetailsHidden` | Details hidden | Whether the title and attendees are hidden from the report's owner. |
| `Transparency` | Busy or free | `Busy`, or `Free` when the event leaves the time free. |
| `AttendeeCount` | Attendees | How many people, rooms and equipment are invited; empty when the details are hidden. |
| `IsRecurring` | Repeats | |
| `SeriesId` | Series ID | The repeating event a changed occurrence belongs to; references `CalendarEvents`. |
| `Source`, `SourceId` | Source, Source ID | The module that owns the event, such as `Scheduling`, and its record. |
| `CalendarId` | Calendar ID | References `Calendars`. |
| `CalendarType` | Calendar type | |
| `StartUtc`, `EndUtc` | Starts, Ends | For a repeating event, its first occurrence. |
| `AllDay` | All day | |
| `DurationMinutes` | Duration (minutes) | |
| `TimeZoneId` | Time zone | |
| `CreatedUtc`, `ModifiedUtc` | Created, Last changed | |
| `CreatedBy` | Created by | References `Users`. |

The `UpdateFrom2Async` migration step adds the database index events are read through (`StartUtc`, `Id`).

## Building on the calendar

- [Scheduling](./scheduling.md) puts appointments on `Appointments` calendars and reads free/busy time from the calendar.
- [Bookings](./bookings.md#bookings-on-the-calendar), with Calendars on, keeps every booking on a `Bookings` calendar and closes booking dates that are busy on a chosen calendar.
