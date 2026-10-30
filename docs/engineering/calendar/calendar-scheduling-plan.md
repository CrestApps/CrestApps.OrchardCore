# Calendar and scheduling platform plan (issue #1197)

Status: plan written 2026-10-30. Progress is tracked in the **Progress** section at the end.

Issue [#1197](https://github.com/CrestApps/CrestApps.OrchardCore/issues/1197) asks for a shared calendar foundation, a scheduling engine on top of it, and the existing Bookings module wired to it, with workflows as a first-class part of each. On top of the issue, the calendar must serve **different kinds of calendars** (a workforce calendar, a team calendar, an office calendar, a room calendar, a bookings calendar, ...) and other modules must be able to add their own kinds and use the calendar for their own purposes without changing the Calendar module.

## What exists today

From a survey of the code (2026-10-30):

- **No calendar module, no recurrence, no iCalendar** anywhere in `src`. No calendar UI library either (only `flatpickr` as a date picker in the Resources module).
- **Bookings** (`CrestApps.OrchardCore.Bookings`, one project) keeps its own weekly opening hours and slot rules on `BookingPart`, a `BookingSlot`/`Booking`/`BookingBalance` document store in the `Bookings` collection, capacity holds under an `IDistributedLock`, and a hand-built month table at **Commerce > Bookings > Calendar**. It has no resources (staff, rooms), no workflow events and no hook other modules can listen to when a booking changes. All times are in the site's time zone.
- **Contact Center Business Hours** has its own `BusinessHoursCalendar` (weekly windows + holiday dates) behind `IBusinessHoursGate`. It is a business-hours rule set, not an event calendar; it stays as it is.
- **Time Zones** maps friendly names to IANA ids and supplies the time zone pickers (`ITimeZoneSelectListProvider`). NodaTime 3.3.6 is already pinned in `Directory.Packages.props`.
- **Callbacks** (`CallbackRequest.ScheduledUtc`) and `OmnichannelActivity.ScheduledUtc` are single timestamps; they are later candidates for an event source, not part of this plan's first phases.

## Naming

The modules are plural, like `Bookings`, `Customers` and `Orders`, so the main record can be called `Calendar` without clashing with its own namespace:

| Project | Kind | Holds |
|---|---|---|
| `CrestApps.OrchardCore.Calendars.Abstractions` | Abstractions | The public contracts: models, calendar type descriptors, services and extension interfaces |
| `CrestApps.OrchardCore.Calendars.Core` | Core | Stores, indexes, recurrence engine, free/busy, iCalendar reader and writer, default services |
| `CrestApps.OrchardCore.Calendars` | Module | Features, admin screens, the calendar view, APIs, workflows, recipes, AI tools |
| `CrestApps.OrchardCore.Scheduling` (+ `.Abstractions`, `.Core`) | Module | Appointment types, resources, working hours, availability, appointments, waitlist, self-service |
| `CrestApps.OrchardCore.Bookings` | Existing module | Gains booking events, a calendar integration feature and workflow events |

`System.Globalization.Calendar` is the only other `Calendar` type; files that import `System.Globalization` alias ours.

## Design: one calendar model, many calendar types

The heart of the flexibility is the **calendar type**. A calendar type is registered in code by any feature, the same way a module registers a content part or a report data source. It says what a calendar of that type is for and how its events behave. Every calendar record names its type. The Calendar module itself never switches on a type name; it reads the type's descriptor.

### Calendar type descriptor

```csharp
services.AddCalendarType("Workforce", type => type
    .WithDisplayName(S["Workforce"])
    .WithDescription(S["Shifts, time off and training for a group of employees."])
    .WithIcon("fa-solid fa-user-clock")
    .WithOwner(CalendarOwnerKind.Role)              // None, User, Role (team) or Resource
    .WithCapabilities(CalendarCapabilities.Attendees | CalendarCapabilities.Recurrence | CalendarCapabilities.AllDay)
    .WithConflictPolicy(CalendarConflictPolicy.AllowOverlaps)
    .AddEventType("Shift", S["Shift"], e => e.Color("#2b8a3e").Busy().RequiresAttendee())
    .AddEventType("TimeOff", S["Time off"], e => e.Color("#e8590c").Busy().AllDayByDefault().NeedsApproval())
    .AddEventType("Training", S["Training"], e => e.Color("#1971c2").Busy()));
```

| Descriptor member | Meaning |
|---|---|
| `Name`, `DisplayName`, `Description`, `Icon` | How the type shows in pickers and on the calendar list |
| `OwnerKind` | Who a calendar of this type belongs to: nobody (a shared company calendar), a user (personal), a role (a team), or a resource (a room, desk area, piece of equipment). Drives the owner picker and default access |
| `Capabilities` | Flags the editor and the services honor: attendees, recurrence, all-day events, free/busy, external sync, public feed |
| `ConflictPolicy` and `DefaultCapacity` | `AllowOverlaps`, or `LimitConcurrent` with a capacity (1 for a room, the desk count for an office floor). Enforced under a distributed lock on every create, move and recurrence change |
| `EventTypes` | The kinds of events a calendar of this type holds, each with a color, busy or free, all-day by default, needs approval (created tentative), requires an attendee, counts toward capacity |
| `Hidden` / `Creatable` | A type a module manages itself (for example the Bookings calendar) can hide the **Add** button so only that module creates calendars of it |

Types are kept in `CalendarOptions` (an `IOptions` of descriptors), read through `ICalendarTypeManager`. Built-in types are registered by Calendar features so a site turns on only what it uses:

| Feature | Types it registers | Notes |
|---|---|---|
| Calendars (base) | `General` (a shared company calendar), `Personal` (owned by a user) | Everything else depends on this |
| Team Calendars | `Team` (owned by a role) with Meeting, On call, Out of office | Members of the role read and add events; a team availability view shows each member's free/busy side by side |
| Resource Calendars | `Room`, `Equipment` | Capacity 1 by default, `LimitConcurrent`; resources can be invited to events from other calendars |
| Workforce Calendars | `Workforce` with Shift, Time off (needs approval), Break, Training | Shifts name the employees as attendees; an employee's free/busy comes from their shifts and time off |
| Office Calendars | `Office` with In office, Desk booking, Room booking, Office closed | Capacity is the number of desks; In office and Desk booking count toward it |
| Bookings (in the Bookings module) | `Bookings` (hidden, created by Bookings) | Proves the extension point from outside the module |
| Scheduling (in the Scheduling module) | `Appointments` | Holds appointment events for staff and rooms |

### Type-specific data and editors

`Calendar` and `CalendarEvent` are `CatalogItem`s, so both carry the `Properties` bag (`Put<T>`, `GetOrCreate<T>`, `Alter<T>`). A module adds its own settings or event data as a typed object in the bag and edits it with a `DisplayDriver<Calendar>` or `DisplayDriver<CalendarEvent>` that returns `null` for other types. The editor screens are driver-built, so a module's fields appear on the right tab without the Calendar module knowing about them.

### Event model

| Concept | Representation |
|---|---|
| Calendar | `Calendar`: name, type, description, color, time zone, owner (user id, role name or resource key), visibility (`Private`, `Shared`, `Public`), sharing entries (user or role with `FreeBusy`, `Read`, `Write` or `Manage`), conflict policy and capacity (defaulted from the type), enabled |
| Calendar event | `CalendarEvent`: calendar id, event type, title, description, location, start and end as **local date-time + IANA time zone** (all-day events store dates, end exclusive), the derived `StartUtc`/`EndUtc`, status (`Tentative`, `Confirmed`, `Cancelled`), transparency (`Busy`, `Free`), visibility, attendees, recurrence, source, provider mappings, created/modified by and when |
| Attendee | `CalendarAttendee`: user id or email + name, or a resource calendar id; role (`Required`, `Optional`, `Resource`); response (`NeedsAction`, `Accepted`, `Tentative`, `Declined`) |
| Recurrence | `CalendarRecurrence`: an RFC 5545 `RRULE` (DAILY, WEEKLY, MONTHLY, YEARLY with INTERVAL, COUNT, UNTIL, BYDAY with ordinals, BYMONTHDAY, BYMONTH, BYSETPOS, WKST), `ExceptionDates`, and the derived `RecurrenceEndUtc` (null when it never ends) |
| Occurrence change | An **override** event: its own `CalendarEvent` with `SeriesId` (the master) and `OriginalStartUtc`. Cancelling one occurrence is an override with status `Cancelled` (or an EXDATE when it has no other change). "This and following" splits the series: the master gets an `UNTIL`, a new master starts at the occurrence |
| Source | `Source` + `SourceId` name the module and record that owns an event (`Scheduling`/appointment id, `Bookings`/booking id). Source-owned events are read-only in the calendar UI unless the source allows edits; the source updates them through `ICalendarEventManager` |
| Provider mapping | `CalendarProviderMapping`: provider name, external calendar id, external event id, etag/change key, last synced |

Times: the event keeps the wall-clock time and the zone, and the UTC instants are recomputed from them on save, so a 9:00 weekly meeting stays at 9:00 across daylight-saving changes. Recurrence is expanded in local time with NodaTime (`DateTimeZoneProviders.Tzdb`), skipping wall-clock times that don't exist and taking the earlier of two that repeat, as RFC 5545 asks.

### Storage

YesSql documents in a `Calendars` collection:

- `CalendarIndex`: ItemId, Name, Type, OwnerKind, OwnerId, Visibility, Enabled.
- `CalendarEventIndex`: ItemId, CalendarId, EventType, Status, StartUtc, EndUtc, `RangeEndUtc` (EndUtc for a single event, RecurrenceEndUtc or max for a series), IsRecurring, SeriesId, OriginalStartUtc, Source, SourceId, Transparency.
- `CalendarEventAttendeeIndex` (one row per attendee): EventId, CalendarId, UserId, ResourceCalendarId, Email, Response, StartUtc, RangeEndUtc. Lets "everything a user attends" and resource bookings be queried without loading all events.
- `CalendarProviderMappingIndex` (one row per mapping): EventId, ConnectionId, ExternalEventId.

A range query loads events whose `StartUtc < to` and `RangeEndUtc > from`, expands recurring masters, applies overrides and EXDATEs, and returns `CalendarOccurrence`s (event + occurrence start/end + whether it is an override). Queries are capped (`CalendarOptions.MaxOccurrencesPerQuery`, default 5,000) so a range can never load an unbounded set.

### Services (the public contracts)

| Interface | What it does |
|---|---|
| `ICalendarTypeManager` | Lists and finds calendar type descriptors and their event types |
| `ICalendarService` | Create, update, delete and find calendars; lists the calendars a user can see; runs `ICalendarHandler`s |
| `ICalendarEventManager` | Create, update, move, cancel and delete events; change one occurrence, this and following, or the whole series; add, remove and update attendees; record a response. Validates, enforces the conflict policy under a lock, runs `ICalendarEventHandler`s and raises after-commit notifications |
| `ICalendarQueryService` | Occurrences in a range for one or more calendars, for a user (owned + attending), or for a resource; honors visibility and access (free/busy-only callers get busy blocks without titles) |
| `ICalendarAvailabilityService` | Free/busy for users, calendars and resources, merging busy occurrences with every `ICalendarBusyTimeSource`; finds free windows of a given length |
| `ICalendarAccessService` + `CalendarPermissions` | Resource-based authorization: `ViewCalendar`, `ViewCalendarFreeBusy`, `EditCalendarEvents`, `ManageCalendar` for a calendar, with an `AuthorizationHandler` that combines the global permissions with owner, sharing and visibility |
| `IICalendarSerializer` | Writes calendars and events to iCalendar (RFC 5545) and reads VEVENTs (with RRULE, EXDATE, RECURRENCE-ID, VTIMEZONE via TZID) |

### Extension points

| Contract | Use |
|---|---|
| `AddCalendarType(...)` | Register a calendar type and its event types |
| `ICalendarHandler` | React to calendars being created, updated, deleted |
| `ICalendarEventHandler` | Validate (`ValidatingAsync` adds errors), and react to created, updated, moved, cancelled, deleted, attendee added and response changed. Runs in the request; after-commit work goes through `ICalendarEventNotifier` |
| `ICalendarEventNotifier` | Raised **after the session commits** (deferred task) with a stable payload; the workflow integration, invitations and provider push listen here |
| `ICalendarBusyTimeSource` | Contribute busy time that isn't stored as events (a module's own records), so free/busy and scheduling see it |
| `ICalendarEventSource` | Contribute read-only occurrences to the calendar view from another module's records without copying them (for example scheduled callbacks) |
| `ICalendarProvider` | An external calendar provider (iCalendar feed, Google, Microsoft 365): connect, pull changes, push changes |
| `DisplayDriver<Calendar>` / `DisplayDriver<CalendarEvent>` | Add type-specific fields to the editors |

## Features

| Feature | Id | Depends on | Adds |
|---|---|---|---|
| Calendars | `CrestApps.OrchardCore.Calendars` | Users | Calendars, events, General and Personal types, admin screens, the calendar view (day, week, month, agenda), permissions, recipes and deployment |
| Team Calendars | `CrestApps.OrchardCore.Calendars.Teams` | Calendars, Roles | Team type and the team availability view |
| Resource Calendars | `CrestApps.OrchardCore.Calendars.Resources` | Calendars | Room and Equipment types, resource attendees |
| Workforce Calendars | `CrestApps.OrchardCore.Calendars.Workforce` | Calendars | Workforce type: shifts, time off with approval |
| Office Calendars | `CrestApps.OrchardCore.Calendars.Office` | Calendars | Office type with desk capacity |
| Calendar Invitations | `CrestApps.OrchardCore.Calendars.Invitations` | Calendars, Email | Invitation, update and cancellation emails with an `.ics` attachment; reply links that record the response |
| Calendar Reminders | `CrestApps.OrchardCore.Calendars.Reminders` | Calendars | Durable "starting soon" and "ended" processing (background task + reminder ledger) used by workflows and notifications |
| Calendar Sharing Feeds | `CrestApps.OrchardCore.Calendars.Feeds` | Calendars | A private iCalendar feed URL per calendar that Google Calendar, Outlook and phones can subscribe to; `.ics` import and export in the admin |
| External Calendars | `CrestApps.OrchardCore.Calendars.External` | Calendars | Provider connections and the sync background task; the built-in iCalendar URL provider (read-only subscription) |
| Calendars API | `CrestApps.OrchardCore.Calendars.Api` | Calendars | `/api/calendars/v1` endpoints |
| Calendar AI Tools | `CrestApps.OrchardCore.Calendars.AI` | Calendars, AI | AI functions: list calendars, find free time, list events, create, move and cancel an event |
| Workflows integration | (startup with `RequireFeatures("OrchardCore.Workflows")`) | | Calendar workflow events and tasks |
| Scheduling | `CrestApps.OrchardCore.Scheduling` | Calendars | Appointment types, resources, working hours, availability, appointments, holds, reminders |
| Scheduling Self-Service | `CrestApps.OrchardCore.Scheduling.SelfService` | Scheduling | Public booking pages, manage links to reschedule or cancel |
| Scheduling Waitlist | `CrestApps.OrchardCore.Scheduling.Waitlist` | Scheduling | Waitlist entries and the slot-available event |
| Scheduling API, AI Tools | `...Scheduling.Api`, `...Scheduling.AI` | Scheduling | Endpoints and AI functions |
| Bookings Calendar | `CrestApps.OrchardCore.Bookings.Calendar` | Bookings, Calendars | Booking events on a Bookings calendar, busy time from a calendar closing slots, backfill |

## Phases

Each phase is a commit (or a few) on one branch and one pull request, per the "one PR per plan" rule. Each phase is useful and testable on its own.

### Phase 1 — Calendar foundation

1. **Projects and contracts.** The three Calendars projects, solution and targets entries, test references. Models, enums, descriptors, `AddCalendarType` builder, service and extension interfaces.
2. **Recurrence engine** (Core): `RecurrenceRule` parser/formatter and `RecurrenceExpander` on NodaTime. Test-first: daily/weekly/monthly/yearly, BYDAY ordinals (`2TU`, `-1FR`), BYSETPOS, COUNT vs UNTIL, EXDATE, DST spring-forward and fall-back in America/New_York and Europe/London, all-day series, the occurrence cap.
3. **Stores and indexes**: `CalendarStore`, `CalendarEventStore`, the four indexes, migration in the `Calendars` collection.
4. **Managers**: calendar and event managers with validation, conflict policy under `IDistributedLock` (`CALENDAR_{id}`), occurrence edits (this / this and following / all), attendees, handlers, after-commit notifier.
5. **Query, free/busy and access**: query service, availability service (free windows), access service and authorization handler, permissions and stereotypes.
6. **Admin**: **Calendars** menu with the calendar view (FullCalendar 6, MIT, vendored as a copy asset), the calendar list (AdminList), calendar editor (driver-built, owner and sharing tabs), event editor (driver-built, recurrence builder, attendees), JSON endpoints the view uses (range load, create, move/resize by drag, quick add).
7. **Built-in types**: the General/Personal types plus the Team, Resource, Workforce and Office features, each with its event types and any type-specific editor (team availability view, workforce shift attendees, office capacity).
8. **Workflows**: events Calendar Event Created, Updated, Cancelled, Attendee Invited, Attendee Response Received, Recurring Occurrence Changed; tasks Create Calendar Event, Update Calendar Event, Cancel Calendar Event, Get Calendar Event, Find Calendar Events, Add Attendee, Remove Attendee, Find Free Time. Triggered from the after-commit notifier, with the event's id, calendar id, type, occurrence start and a `ChangedBy` that lets an event activity skip changes made by workflows (loop guard).
9. **Reminders**: background task every minute; a `CalendarReminderLedger` (unique key event + occurrence + kind) so a trigger fires once even after a restart or on two nodes; Calendar Event Starting Soon (minutes-before setting on the event activity, ledger stores the offsets that workflows registered) and Calendar Event Completed. Rescheduling changes the key, so an old reminder can never fire.
10. **Recipes and deployment**: `Calendars` step (calendars with their events) and deployment step; JSON schema.
11. **Tests**: recurrence, DST, manager validation and conflicts (including concurrent creates on SQLite), occurrence edits, access rules, query/free-busy, iCalendar round trip, workflow triggers.

### Phase 2 — Scheduling

1. **Projects** and the `Appointments` calendar type.
2. **Resources**: `SchedulingResource` (staff member = user, room, equipment, or any other kind a module registers) linked to the calendar that holds its commitments (a Personal calendar for staff, a Room/Equipment calendar for rooms). Working hours: weekly windows (several per day, so breaks are the gaps) and date overrides; or "available only during shifts" read from a Workforce calendar.
3. **Appointment types**: duration, slot interval, buffer before/after, minimum notice, booking horizon, daily limit per resource, eligible resources, assignment (`AnyAvailable`, `RoundRobin`, `Specific`, `AllTogether` for collective availability), capacity (group sessions), requires confirmation, location, color.
4. **Availability**: `ISchedulingAvailabilityService.FindSlotsAsync(type, from, to, timeZone, resource?)` = working hours − busy time (calendar free/busy incl. busy sources, appointment buffers, holds) − notice/horizon/limits, in the customer's time zone.
5. **Appointments**: `Appointment` (type, resource(s), customer name/email/phone/user/contact, status `Held`, `Requested`, `Confirmed`, `Completed`, `NoShow`, `Cancelled`, `Expired`, hold expiry, calendar event id, manage token, notes, `Properties`). `IAppointmentService`: hold, request, book (confirm), reschedule, cancel, complete, mark no-show. Every reservation re-checks availability under a lock per resource (`SCHEDULING_RESOURCE_{id}`, sorted to avoid deadlocks), writes the calendar event (source `Scheduling`) and the appointment in one session.
6. **Background**: expire holds (Reservation Hold Expired), appointment reminders and completion through the Calendar reminder ledger.
7. **Admin**: Scheduling menu with Appointments (AdminList with status filter, detail with actions), Appointment types, Resources (working hours editor); appointment events show on the calendar view.
8. **Workflows**: Appointment Requested, Confirmed, Rescheduled, Cancelled, Starting Soon, Completed, Marked No-Show, Reservation Hold Expired, Waitlist Slot Available; tasks Find Appointment Slots, Book Appointment, Confirm, Reschedule, Cancel, Get Appointment, Assign Resource, Add to Waitlist.
9. **Self-service**: a public page per appointment type (pick a day, pick a time in the visitor's time zone, enter details, book), confirmation page, signed manage link to reschedule or cancel, emails when Email is on.
10. **Waitlist**: entries per type and date range; a cancellation that frees a slot raises Waitlist Slot Available for the oldest matching entry.
11. **API and AI tools**: `/api/scheduling/v1` (types, slots, appointments) and AI functions (find slots, book, find my appointments, reschedule, cancel), all through `IAppointmentService`.
12. **Tests**: availability with hours, breaks, buffers, notice, horizon, limits, DST; concurrent booking of the same slot on SQLite (one wins); round robin and collective; hold expiry; reschedule moves the calendar event and its reminders.

### Phase 3 — Bookings on the calendar

1. **Booking events in Bookings**: `IBookingEventHandler` (Created, Confirmed, Updated/Rebooked, Cancelled, Completed, NoShow, Expired, BalancePaid) raised by `BookingService`; no behavior change.
2. **Bookings workflow events**: Booking Created, Confirmed, Updated, Cancelled, Completed, Payment Received (deposit or balance paid), Payment Failed (balance charge failed) and Refunded (cancellation with a refund), from those handlers. Only operations Bookings already performs.
3. **Bookings Calendar feature** (`RequireFeatures` Calendars): registers the hidden `Bookings` calendar type; keeps one calendar per bookable product (or one shared, a setting); every held/requested/confirmed booking is an event (source `Bookings`, tentative while held or requested), moved on rebook, cancelled on cancel or expiry. A **Sync existing bookings** button (and a background pass) backfills bookings with no event.
4. **Busy time into Bookings**: an optional **Block with calendar** setting on `BookingPart` (a calendar id); time-slot checks and searches treat busy occurrences in that calendar as closed. Bookings stays independent of Scheduling.
5. **The existing month table** at Commerce > Bookings > Calendar links to the shared calendar view when the feature is on, and stays as it is otherwise.
6. **Tests**: booking lifecycle → calendar events; backfill idempotent; busy time closes slots; existing Bookings tests unchanged.

### Phase 4 — Interoperability and self-service hardening

1. **iCalendar**: `.ics` export per calendar and event, `.ics` import into a calendar (UID-based upsert), private subscription feed per calendar (rotatable token).
2. **Provider abstraction and sync**: `CalendarConnection` (provider, external calendar, credentials reference, sync token, status, last error, backoff), `ICalendarProvider` with `PullAsync`/`PushAsync`, sync background task with retry/backoff, idempotent upsert through provider mappings, external deletions → cancellations, a "provider wins / local wins / newest wins" policy per connection. A provider being down marks the connection and never blocks the internal calendar.
3. **Built-in provider**: iCalendar URL subscription (read-only).
4. **Google Calendar and Microsoft 365** providers (OAuth sign-in per user, tenant-level app registration settings, two-way sync with sync tokens / delta queries). **Needs app registrations and test accounts to verify**; built after the rest and marked as such until verified live.

### Phase 5 — Ecosystem

1. **Reports**: a Calendar data source (events and occurrences) and a Scheduling data source (appointments, utilization) for the Report Designer.
2. **Contact Center / CRM**: scheduled callbacks as an `ICalendarEventSource`, so agents see them on their calendar; meeting follow-ups from CRM through the workflow tasks.
3. **AI agents**: the Calendar and Scheduling AI tools (phase 1/2) on profiles; the parked AI booking tools (`claude/ai-booking-tools`) stay on Bookings and can use the calendar's busy time.

## Decisions

- **CD1. Plural module names** (`Calendars`), so the record is `Calendar` and matches `Bookings`/`Booking`. The issue's `CrestApps.OrchardCore.Calendar` name would make every `Calendar` reference ambiguous with its namespace.
- **CD2. Calendar types are code-registered**, not admin-defined, because a type carries behavior (owner kind, conflict policy, event types, editors). Administrators create calendars of the types their features turn on.
- **CD3. Our own RRULE engine on NodaTime** instead of a third-party iCalendar library: the subset RFC 5545 needs for calendars is small, NodaTime is already pinned, and the engine is fully unit-testable. The iCalendar reader/writer is ours too.
- **CD4. FullCalendar 6 (MIT)** for the day/week/month/agenda view, vendored as a copy asset of its global build, not a CDN.
- **CD5. Overrides are separate events** (`SeriesId` + `OriginalStartUtc`), the model Google and Microsoft use, so sync maps one-to-one.
- **CD6. Business Hours stays in Contact Center.** It is a rule set for opening hours; a later change could let a business-hours calendar read holidays from a calendar.
- **CD7. Google and Microsoft 365 come last** and are only described as working once verified with real accounts.

## Progress

- 2026-10-30: plan written.
