---
name: crestapps-calendar-types
description: >
  Skill for extending the CrestApps.OrchardCore Calendars module (CrestApps.OrchardCore.Calendars, Abstractions and
  Core) from another module: registering a calendar type and its event types with services.AddCalendarType (owner kind,
  capabilities, visibility, LimitConcurrent capacity, ManagedByCode, event type defaults), creating and finding a
  module-managed calendar by Source/SourceId, writing events through ICalendarEventManager with CalendarChangeOptions,
  adding type-specific fields with DisplayDriver<Calendar> / DisplayDriver<CalendarEvent> and the Properties bag,
  contributing busy time (ICalendarBusyTimeSource, AddCalendarBusyTimeSource) or read-only occurrences
  (ICalendarEventSource, AddCalendarEventSource), validating and reacting with ICalendarHandler / ICalendarEventHandler,
  acting after commit with ICalendarEventObserver, reminders (ICalendarReminderOffsetProvider,
  ICalendarReminderObserver), where external providers live (see crestapps-calendar-provider), and the pitfalls: the Calendar name clashing with
  System.Globalization.Calendar, ICalendarService versus Orchard Core's ICalendarManager, wall-clock DateTime fields
  needing WallClockDateTimeJsonConverter, never setting StartUtc/EndUtc by hand, and overlap checks under locks. Use
  this skill whenever the request is to "add a calendar type", "put <records> on the calendar", "show <records> on the
  calendar", "make <records> count as busy", "react to calendar event changes", or integrate a module with Calendars or
  Scheduling.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Extending the Calendars module

The **Calendars** module keeps calendars and events in the `Calendars` YesSql collection. What a calendar is for comes
from its **calendar type**, a `CalendarTypeDescriptor` that a feature registers in code. The module never switches on a
type name: it reads the descriptor. Other modules add types, put their records on calendars, make their records count
as busy time, and react to event changes, all without changing the Calendars module.

Read first:

- Technical Manual: `src/CrestApps.Docs/docs/modules/calendars.md` (model, services, concurrency, workflows, API).
- Contracts: `src/Abstractions/CrestApps.OrchardCore.Calendars.Abstractions` (`CalendarServiceCollectionExtensions`,
  `CalendarTypeBuilder`, `CalendarEventTypeBuilder`, `Services/`, `Models/`).
- Worked examples in the repo:
  - Built-in types: `src/Modules/CrestApps.OrchardCore.Calendars/Startup.cs`, `TeamsStartup.cs`, `ResourcesStartup.cs`,
    `OfficeStartup.cs`. There is no built-in workforce type: a workforce management module registers its own
    (shifts with `RequiresAttendee()`, time off with `NeedsApproval()`).
  - A module-managed type from another module: `src/Modules/CrestApps.OrchardCore.Bookings/CalendarStartup.cs` (a
    Bookings startup with `[RequireFeatures(CalendarsConstants.Features.Calendars)]`) and
    `Calendar/BookingCalendarHandler.cs` (one event per booking), `Calendar/BookingCalendarSlotFilter.cs` (reading busy
    time).
  - Scheduling: `src/Modules/CrestApps.OrchardCore.Scheduling/Startup.cs` (the `Appointments` type) and
    `src/Core/CrestApps.OrchardCore.Scheduling.Core/Services/DefaultAppointmentService.cs` (writes events with
    `SkipConflictCheck` after checking under its own locks).

## 1. Reference and depend

- Project reference: `CrestApps.OrchardCore.Calendars.Abstractions` (and `.Core` only if you need `CalendarTime`, the
  iCalendar reader/writer or `ICalendarProvider`).
- Feature dependency: `CrestApps.OrchardCore.Calendars` (`CalendarsConstants.Features.Calendars`), either in the
  feature's `Dependencies` when your feature can't work without calendars or, for an optional integration (the
  recommended way), a startup of your own feature with `[RequireFeatures(CalendarsConstants.Features.Calendars)]`, as
  Bookings' `CalendarStartup` does. Don't add a separate "X Calendar" feature just for the integration: the startup runs
  whenever both features are on, and anything outside it (a controller, a view) resolves the integration's services
  with `GetService` and treats `null` as "Calendars is off".

## 2. Register a calendar type

```csharp
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using OrchardCore.Localization;

services.AddCalendarType("Vehicles", type => type
    .WithDisplayName(LocalizationSource.Create<Startup>("Vehicle"))
    .WithDescription(LocalizationSource.Create<Startup>("A pool vehicle that one driver uses at a time."))
    .WithIcon("fa-solid fa-car")
    .WithColor("#1971c2")
    .WithOwner(CalendarOwnerKind.Resource)          // None, User (owner id = user id), Role (owner id = role name), Resource
    .WithVisibility(CalendarVisibility.Public)       // Private (default), Shared (free/busy), Public
    .LimitConcurrent(1)                              // refuse a busy event when 1 busy event already overlaps
    .WithPosition(70)
    .AddEventType("Trip", LocalizationSource.Create<Startup>("Trip"), e => e.Color("#1971c2").RequiresAttendee().Lasts(240))
    .AddEventType("Service", LocalizationSource.Create<Startup>("Service"), e => e.Color("#868e96").AllDayByDefault()));
```

- Event type options: `Busy()` / `Free()` (free also leaves capacity), `AllDayByDefault()`, `Lasts(minutes)`,
  `NeedsApproval()` (created `Tentative`; someone with Manage confirms), `RequiresAttendee()`, `IgnoresCapacity()`.
- A type with no event types gets the default `Event` type (`CalendarsConstants.EventTypes.Default`).
- `WithCapabilities(...)` limits what the editor offers (`Attendees`, `Recurrence`, `AllDay`, `FreeBusy`,
  `ExternalSync`, `Feed`; default `All`).
- Calling `AddCalendarType` with an existing name **extends** that type (it's `GetOrAddType` under the hood), so a
  feature can add event types to a type another feature registered. Pick a unique name for a new type.
- Add a constant for the type name; it is stored on every calendar, so never rename it.

## 3. A calendar your module manages

For a type only your module creates calendars of (like Bookings or Appointments):

```csharp
services.AddCalendarType("Vehicles", type => type
    // ...
    .ManagedByCode());   // Creatable = false: not in the Add calendar menu
```

Create it once and find it again by source:

```csharp
var calendar = await _calendarService.FindBySourceAsync("Fleet", vehicleId);

if (calendar is null)
{
    calendar = await _calendarService.NewAsync("Vehicles");   // null when the type isn't registered (feature off)
    calendar.Name = vehicle.Name;
    calendar.Source = "Fleet";
    calendar.SourceId = vehicleId;

    var result = await _calendarService.CreateAsync(calendar, new CalendarChangeOptions { Actor = "Fleet" });
}
```

## 4. Put your records on the calendar as events

Write events through `ICalendarEventManager`, never through a store, so validation, handlers, the overlap check and the
after-commit observers run:

```csharp
var calendarEvent = await _eventManager.NewAsync(calendar, "Trip");   // applies the event type's defaults

calendarEvent.Title = trip.Title;
calendarEvent.Start = CalendarTime.ToLocal(trip.StartsUtc, calendarEvent.TimeZoneId);  // wall-clock in the event's zone
calendarEvent.End = CalendarTime.ToLocal(trip.EndsUtc, calendarEvent.TimeZoneId);
calendarEvent.Source = "Fleet";
calendarEvent.SourceId = trip.Id;
calendarEvent.Editable = false;   // only your module changes it; the calendar UI shows it read-only

var result = await _eventManager.CreateAsync(calendarEvent, new CalendarChangeOptions
{
    Actor = "Fleet",
    SendInvitations = false,
});

if (!result.Succeeded)
{
    // result.Errors: CalendarError(Field, Message, Code); Code == CalendarError.ConflictCode when the calendar is full.
}
```

- Find your events later with `FindBySourceAsync(source, sourceId)`; update with
  `UpdateAsync(calendarEvent, target => { ... }, options)` (the change is rolled back on the object when refused);
  cancel with `CancelAsync`; delete with `DeleteAsync`.
- Repeating events: `UpdateOccurrenceAsync` / `CancelOccurrenceAsync` with `CalendarEditScope` (`ThisOccurrence`,
  `ThisAndFollowing`, `AllOccurrences`) and the occurrence's original start in UTC.
- `CalendarChangeOptions.SkipConflictCheck = true` only when your module already checked availability under its own
  lock (Scheduling does). `Notify = false` keeps workflows and other observers from hearing about the change.
- Do the event write inside the same request/session as your record, so both commit or neither does (Bookings writes
  its event from `IBookingHandler.SavingAsync`).

## 5. Type-specific fields

Store your data in the `Properties` bag (`Calendar` and `CalendarEvent` are `CatalogItem`s: `Put<T>`, `TryGet<T>`,
`GetOrCreate<T>`, `Alter<T>`) and edit it with a driver that only answers for your type:

```csharp
internal sealed class VehicleSettingsDisplayDriver : DisplayDriver<Calendar>
{
    public override IDisplayResult Edit(Calendar calendar, BuildEditorContext context)
    {
        if (calendar.Type != "Vehicles")
        {
            return null;
        }

        return Initialize<VehicleSettingsViewModel>("VehicleSettings_Edit", model =>
        {
            var settings = calendar.GetOrCreate<VehicleSettings>();
            model.Plate = settings.Plate;
        }).Location("Content:3%General;1");
    }

    // UpdateAsync: same type check, read the model, calendar.Put(settings), return Edit(...).
}

services.AddDisplayDriver<Calendar, VehicleSettingsDisplayDriver>();
```

Tabs of the calendar editor: `General;1`, `Owner;2`, `Sharing;3`, `Overlaps;4`, `Subscription;5`. Event editor:
`General;1`, `Repeat;2`, `Attendees;3`. See `ResourceCalendarSettingsDisplayDriver` and
`OfficeCalendarSettingsDisplayDriver`.

## 6. Make your records count as busy time (without copying them)

```csharp
internal sealed class FleetBusyTimeSource : ICalendarBusyTimeSource
{
    public string Name => "Fleet";

    public async Task<IEnumerable<CalendarBusyInterval>> GetBusyTimeAsync(CalendarBusyQuery query, CancellationToken cancellationToken = default)
    {
        // Answer only for what was asked: query.CalendarIds, query.UserIds, between query.FromUtc and query.ToUtc.
        // Query an index; never load every record.
        return trips.Select(trip => new CalendarBusyInterval
        {
            StartUtc = trip.StartsUtc,
            EndUtc = trip.EndsUtc,
            UserId = trip.DriverId,
            Source = Name,
        });
    }
}

services.AddCalendarBusyTimeSource<FleetBusyTimeSource>();
```

Free/busy (`ICalendarAvailabilityService`), the team availability page, the API's free-time endpoint and Scheduling's
open times all include it (unless a caller sets `IncludeBusyTimeSources = false`).

## 7. Show your records on the calendar view (read-only)

```csharp
internal sealed class FleetEventSource : ICalendarEventSource
{
    public string Name => "Fleet";

    public async Task<IEnumerable<CalendarOccurrence>> GetOccurrencesAsync(CalendarOccurrenceQuery query, CancellationToken cancellationToken = default)
    {
        // Build an unsaved CalendarEvent per record with Editable = false, Source = Name, and return occurrences that overlap
        // query.FromUtc..query.ToUtc for query.CalendarIds / query.AttendeeUserIds. Respect query.Viewer.
    }
}

services.AddCalendarEventSource<FleetEventSource>();
```

Use a source when the records already live elsewhere and must not be duplicated; use real events (section 4) when they
need overlap checks, invitations, feeds or workflows.

## 8. Validate and react

| Contract | When it runs | Use it for |
| --- | --- | --- |
| `ICalendarHandler` (`CalendarHandlerBase`) | In the request: `ValidatingAsync`, `CreatedAsync`, `UpdatedAsync`, `DeletingAsync` | Rules for calendars of your type; cleaning up your records when a calendar is deleted. |
| `ICalendarEventHandler` (`CalendarEventHandlerBase`) | In the request: `ValidatingAsync` (add to `context.Errors` to refuse), `SavingAsync` (adjust), `SavedAsync`, `DeletingAsync` | A type's own rules, such as "a trip needs a driver with a licence". `context.CalendarType` / `context.EventType` give the descriptors. |
| `ICalendarEventObserver` | **After the session commits**, in a new scope | Emails, workflows, calling another system. Never acts on a rolled-back change. Must not throw for control flow; failures are logged. |
| `ICalendarReminderOffsetProvider` + `ICalendarReminderObserver` | Calendar Reminders feature, once per occurrence and offset | "Starting soon" and "completed" moments. |

Register handlers with `services.AddScoped<ICalendarEventHandler, MyHandler>()` (same for the others). Check the type in
every handler: handlers run for every calendar.

## 9. External providers

External calendar providers (`ICalendarProvider`, `IIncrementalCalendarProvider`, `IWritableCalendarProvider` in
`CrestApps.OrchardCore.Calendars.Core.External`) have their own skill: `crestapps-calendar-provider`. Only the iCalendar
address provider (`ics`) ships today; Google Calendar and Microsoft 365 are separate work.

## 10. Check who may see a schedule or an event

Check only the broad permission with the resource; `CalendarEventAuthorizationHandler` grants it through the Own permission
for the user's own schedule or event, and through calendar sharing for events on calendars the user can read. Never check the
Own permission yourself.

```csharp
await authorizationService.AuthorizeAsync(User, CalendarPermissions.ListCalendarEvents, new CalendarSchedule(userId)); // or CalendarSchedule.Everyone
await authorizationService.AuthorizeAsync(User, CalendarPermissions.ViewCalendarEvents, calendarEvent); // false: show it as busy
```

## Pitfalls

- **`Calendar` clashes with `System.Globalization.Calendar`.** Any file that imports `System.Globalization` (for
  `CultureInfo`) and uses our model must alias it: `using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;`.
  Inside a namespace that has its own `Calendar` segment (for example `CrestApps.OrchardCore.Bookings.Calendar`),
  qualify it as `Calendars.Models.Calendar`.
- **Ours is `ICalendarService`, not Orchard Core's `ICalendarManager`.** `OrchardCore.Localization.ICalendarManager` is the
  culture calendar system (Gregorian, Hijri, ...). Calendars, events and types are `ICalendarService`,
  `ICalendarEventManager` and `ICalendarTypeManager`.
- **Wall-clock `DateTime`s.** `CalendarEvent.Start`/`End` and `Recurrence.ExceptionDates` are local times in the
  event's `TimeZoneId`, with an unspecified kind. YesSql would treat an unspecified `DateTime` as server-local time and
  shift it, so those properties carry `[JsonConverter(typeof(WallClockDateTimeJsonConverter))]`
  (`WallClockDateTimeListJsonConverter` for lists). Any wall-clock `DateTime` you add to a model, or to a typed object
  in the `Properties` bag, needs the same converter. Store instants as `...Utc` with `DateTimeKind.Utc`.
- **Never set `StartUtc`/`EndUtc` yourself.** They are recomputed from the wall-clock times and the zone on every save,
  which keeps a 09:00 weekly meeting at 09:00 across daylight saving. Convert with `CalendarTime.ToLocal` /
  `CalendarTime.ToUtc` (Core). All-day events store dates with an exclusive end.
- **Overrides are separate events.** A changed occurrence is its own `CalendarEvent` with `SeriesId` and
  `OriginalStartUtc`. Don't edit a master's rule to move one occurrence; use `UpdateOccurrenceAsync`.
- **Overlap limits are enforced under locks.** `LimitConcurrent` calendars are checked under a distributed lock
  `CALENDAR_{id}` and saved with a `BookingVersion` concurrency bump. If your module books under its own lock (like
  Scheduling's `SCHEDULING_RESOURCE_{id}`), check availability there and write with `SkipConflictCheck = true`; don't
  take both locks in different orders.
- **A disabled feature removes the type.** While the registering feature is off, `ICalendarService.NewAsync(type)`
  returns `null` and `ICalendarTypeManager.FindType(type)` finds nothing, though calendars of the type stay stored.
  Guard for it.
- **Bounded queries.** `CalendarOptions` caps a query at 400 days, 5,000 events and 5,000 occurrences. Ask for the
  range you need.
- **Docs.** A new calendar type is a user-facing change: update the User Manual (`docs/user-manual/calendars/`) and the
  Technical Manual type table in `docs/modules/calendars.md`, plus the changelog.
