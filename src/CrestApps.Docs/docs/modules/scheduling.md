---
sidebar_label: Scheduling
title: Scheduling
description: "Appointment types, staff, rooms and equipment with working hours or shifts, availability from the shared calendar, concurrency-safe booking, holds, a waitlist, public booking pages with manage links, workflows, an HTTP API and AI tools."
user_manual:
  - user-manual/calendars/scheduling
---

The **Scheduling** module books appointments with staff, rooms and equipment. It works out open times from each resource's working hours (or its shifts on a calendar that holds **Shift** events, such as one a workforce management module provides) less its busy time on the shared [calendar](./calendars.md), books them so a resource can never be taken twice, and puts every appointment on the resource's calendar. The admin screens are described in the [User Manual](../user-manual/calendars/scheduling.md).

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.Scheduling` |
| **Abstractions** | `CrestApps.OrchardCore.Scheduling.Abstractions` (models, `IAppointmentService`, `ISchedulingAvailabilityService`, `IAppointmentObserver`, permissions) |
| **Core** | `CrestApps.OrchardCore.Scheduling.Core` (stores, availability, booking) |
| **Category** | Calendar |
| **YesSql collection** | `Scheduling` |

## Features

| Feature | ID | Depends on | Adds |
| --- | --- | --- | --- |
| Scheduling | `CrestApps.OrchardCore.Scheduling` | `CrestApps.OrchardCore.Calendars` | Appointment types, resources, availability, appointments, holds, the waitlist, the **Appointments** calendar type and the admin screens under **Calendars** |
| Scheduling Self-Service | `CrestApps.OrchardCore.Scheduling.SelfService` | Scheduling | Public booking pages (`book/{slug}`) and manage links (`book/manage/{id}/{token}`) |
| Scheduling API | `CrestApps.OrchardCore.Scheduling.Api` | Scheduling | The `/api/scheduling/v1` endpoints |

Workflow activities are registered when `OrchardCore.Workflows` is enabled; **Appointment Starting Soon** also needs Calendar Reminders (`CrestApps.OrchardCore.Calendars.Reminders`). The [AI tools](#ai-tools) are registered when `CrestApps.OrchardCore.AI` is enabled.

## Permissions

| Permission | Key | Default roles | Grants |
| --- | --- | --- | --- |
| Manage scheduling | `ManageScheduling` | Administrator | Set up appointment types and resources. |
| Manage appointments | `ManageAppointments` | Administrator, Editor | Book, change and cancel appointments for anyone; manage the waitlist. |
| View appointments | `ViewAppointments` | Administrator, Editor | See appointments. |

`ManageScheduling` implies `ManageAppointments`, which implies `ViewAppointments`.

## Model

All records live in the `Scheduling` collection.

- **`SchedulingResource`**: `Name`, `Kind` (`Staff`, `Room`, `Equipment`, `Other`), `UserId` (a staff member's user, whose own calendars also make them busy), `CalendarId` (the calendar appointments go on, and whose events make the resource busy), `TimeZoneId`, `AvailabilityMode` (`WorkingHours` or `Shifts`), `ShiftCalendarId`, `WorkingHours` (`SchedulingWorkingHours`: `Day`, `Start`, `End` as `HH:mm`; several per day, so breaks are the gaps), `DateOverrides` (`SchedulingDateOverride`: `Date`, `Closed`, `Start`, `End`), `Enabled` and `BookingVersion`. Saving a resource without a calendar creates one of the `Appointments` type with `Source = Scheduling` and `SourceId` set to the resource.
- **`AppointmentType`**: `Name`, `Description`, `Slug` (the booking page address), `DurationMinutes` (30), `SlotIntervalMinutes` (30), `BufferBeforeMinutes`, `BufferAfterMinutes`, `MinimumNoticeMinutes` (60), `HorizonDays` (60), `MaxPerDay` (0 for no limit), `Capacity` (1; more makes a group session), `ResourceIds`, `Assignment`, `RequiresConfirmation`, `HoldMinutes` (10), `AllowSelfService`, `Location`, `Color`, `Enabled`.
- **`AppointmentAssignment`**: `AnyAvailable` (the first free resource in the list), `RoundRobin` (the free resource with the fewest recent appointments) or `AllTogether` (every resource of the type at once, such as a doctor and a room).
- **`Appointment`**: `AppointmentTypeId`, `ResourceIds`, `StartUtc`, `EndUtc`, `TimeZoneId`, `Status`, `HoldExpiresUtc`, `CustomerName`, `CustomerEmail`, `CustomerPhone`, `UserId`, `Notes`, `CalendarEventId`, `ManageToken` (the secret in the manage link), `Source` (`Admin`, `SelfService`, `Api`, `AI` or `Workflow`), `CancelReason`, created and modified stamps.
- **`AppointmentStatus`**: `Held`, `Requested`, `Confirmed`, `Completed`, `NoShow`, `Cancelled`, `Expired`.
- **`WaitlistEntry`**: `AppointmentTypeId`, `FromUtc`, `ToUtc`, `CustomerName`, `CustomerEmail`, `CustomerPhone`, `Status` (`Waiting`, `Notified`, `Booked`, `Removed`), `CreatedUtc`, `NotifiedUtc`.

## Availability

`ISchedulingAvailabilityService.FindSlotsAsync(type, fromUtc, toUtc, resourceId)` returns `AppointmentSlot`s (`StartUtc`, `EndUtc`, the free `ResourceIds`, and `Remaining` places for a group session):

1. The window is cut to the type's minimum notice and booking horizon.
2. For each eligible resource, the open hours are its weekly working hours less its date overrides, in the resource's time zone, or in `Shifts` mode the **Shift** events on its shift calendar (`ShiftCalendarId`, any calendar that holds a `Shift` event type, such as one a workforce management module registers) that name its user as an attendee.
3. Busy time comes from `ICalendarAvailabilityService.GetBusyAsync` for the resource's calendar and its user, which includes every [busy-time source](./calendars.md#extension-points). Each busy interval is widened by the type's buffers.
4. Start times step by `SlotIntervalMinutes`. A time is offered when the whole appointment fits, the daily limit isn't reached, and, for a group session, the session has places left.
5. With `AllTogether`, a time is offered only when every resource is free.

## Booking and concurrency

`IAppointmentService` is the one way to change appointments. `BookAsync(AppointmentRequest)` books (or with `Hold = true` holds for the type's `HoldMinutes`), `ConfirmAsync` confirms a held or requested appointment, `RescheduleAsync` moves one, `CancelAsync` cancels one, `CompleteAsync(appointment, noShow)` records that it took place or that the customer didn't come, and `ExpireHoldsAsync` ends holds that ran out. Results are `AppointmentResult` (`Succeeded`, `Errors`, and `Conflict` when the time was taken).

Every booking and reschedule:

1. Takes a distributed lock `SCHEDULING_RESOURCE_{id}` on each candidate resource, in id order so two requests never deadlock.
2. Checks availability again under the locks.
3. Picks the resource by the type's assignment.
4. Raises each chosen resource's `BookingVersion` and saves it with YesSql's concurrency check, so two nodes can't book the same resource for the same time.
5. Writes the appointment and its calendar event (`Source = Scheduling`, `SourceId` = the appointment; tentative while held or requested, confirmed once booked) in the same session. The calendar write uses `SkipConflictCheck`, because Scheduling has already checked under its own locks.

A group session (`Capacity` > 1) shares one calendar event per time, with `SourceId = session:...`. A new booking is `Confirmed`, or `Requested` when the type asks for confirmation. Cancelling or moving an appointment frees its time: the oldest waiting `WaitlistEntry` whose range covers that time is marked `Notified` and the **Waitlist Slot Available** change is raised.

`AppointmentHoldsBackgroundTask` runs every minute and calls `ExpireHoldsAsync`: each expired hold is set to `Expired` and its calendar event is cancelled (for a group session, the customer is taken off the shared event).

## Extension points

| Contract | Use |
| --- | --- |
| `IAppointmentService` | Book, confirm, reschedule, cancel and complete appointments from another module. |
| `ISchedulingAvailabilityService` | Find open times for an appointment type. |
| `IAppointmentObserver` | `OnChangedAsync(AppointmentChange)` after the change commits: `Kind` (`Held`, `Requested`, `Confirmed`, `Rescheduled`, `Cancelled`, `Completed`, `NoShow`, `HoldExpired`, `WaitlistSlotAvailable`), `Appointment`, `AppointmentType`, `PreviousStartUtc`, `WaitlistEntry`, `Actor`. The workflow integration listens here. |
| Calendar seams | Appointments are calendar events, so `ICalendarEventObserver`, the calendar workflows and free/busy all see them. Busy time from an `ICalendarBusyTimeSource` closes slots. |

## Self-service

With Scheduling Self-Service on, an appointment type with **Customers can book it themselves** (`AllowSelfService`) and a slug gets a public page at `/book/{slug}`: pick a day, pick an open time (shown in the site's time zone), enter a name, email, phone and notes, and book. The customer lands on their manage page, `/book/manage/{id}/{token}`, whose secret `ManageToken` lets them move the appointment to another open time or cancel it without signing in. A disabled type, or one without self-service, returns `404`.

Scheduling sends no email of its own. To send the confirmation and the manage link, add a workflow on **Appointment Confirmed** (or **Appointment Requested**) with a **Send Email** task; the input has `AppointmentId` and `ManageToken` to build the link.

## Workflows

Activities are in the **Scheduling** category.

| Event | Raised when |
| --- | --- |
| Appointment Requested (`AppointmentRequestedEvent`) | A booking waits for confirmation. |
| Appointment Confirmed (`AppointmentConfirmedEvent`) | An appointment is booked or confirmed. |
| Appointment Rescheduled (`AppointmentRescheduledEvent`) | An appointment moves. `PreviousStartUtc` holds the old start. |
| Appointment Cancelled (`AppointmentCancelledEvent`) | An appointment is cancelled. |
| Appointment Completed (`AppointmentCompletedEvent`) | An appointment is marked as having taken place. |
| Appointment Marked No-Show (`AppointmentNoShowEvent`) | The customer didn't come. |
| Reservation Hold Expired (`AppointmentHoldExpiredEvent`) | A hold ran out. |
| Waitlist Slot Available (`WaitlistSlotAvailableEvent`) | A freed time matched a waiting customer. |
| Appointment Starting Soon (`AppointmentStartingSoonEvent`) | An appointment starts within **Minutes before the start**. Needs Calendar Reminders. |

Each event can be limited to some appointment types (`AppointmentTypeIds`, empty for all). The input holds `Appointment` (the appointment's fields), `AppointmentId`, `Id`, `AppointmentTypeId`, `AppointmentTypeName`, `ResourceIds`, `StartUtc`, `EndUtc`, `Status`, `CustomerName`, `CustomerEmail`, `CustomerPhone`, `UserId`, `Notes`, `CalendarEventId`, `ManageToken`, `Source`, `CancelReason`, `Actor` and `PreviousStartUtc`. **Waitlist Slot Available** holds `AppointmentTypeId`, `AppointmentTypeName`, `WaitlistEntryId`, `CustomerName`, `CustomerEmail`, `CustomerPhone` and `StartUtc` (the freed time). Times are ISO 8601 UTC.

Tasks take Liquid fields; each can rename its output with **OutputName**.

| Task | Fields | Outcomes | Output |
| --- | --- | --- | --- |
| Find Appointment Slots | `AppointmentTypeId`, `From`, `To` | Found, None | `AppointmentSlots` |
| Book Appointment | `AppointmentTypeId`, `Start`, `CustomerName`, `CustomerEmail`, `CustomerPhone`, `Notes` | Done, Taken, Failed | `Appointment` |
| Confirm Appointment | `AppointmentId` | Done, Failed | `Appointment` |
| Reschedule Appointment | `AppointmentId`, `Start` | Done, Taken, Failed | `Appointment` |
| Cancel Appointment | `AppointmentId`, `Reason` | Done, Failed | `Appointment` |
| Get Appointment | `AppointmentId` | Found, NotFound | `Appointment` |
| Add to Waitlist | `AppointmentTypeId`, `From`, `To`, `CustomerName`, `CustomerEmail`, `CustomerPhone` | Done, Failed | |

Appointments booked by a workflow have `Source = Workflow`.

## Recipes and deployment

The **Scheduling** deployment step exports the scheduling resources and appointment types, with **Include appointments** every appointment (with the customer's name, email and phone, but without its manage link), and with **Include the waitlist** the waitlist. Add the **Calendars** step too (with events when appointments are included), so the resources' calendars and the appointments' calendar events come along.

The `Scheduling` recipe step reads the same shape back: `Resources`, `AppointmentTypes`, `Appointments` and `Waitlist`, each created or, when its `itemId` matches a stored one, replaced in place. Run it after the `Calendars` step. An imported appointment keeps the manage link it already had on the site, or gets a new one. Property names are camelCase and enums are written by name (a number is read too).

```json
{
  "steps": [
    {
      "name": "Scheduling",
      "Resources": [
        { "itemId": "drsmith00000001", "name": "Dr. Smith", "kind": "Staff", "calendarId": "drsmithcal00001", "timeZoneId": "America/New_York",
          "availabilityMode": "WorkingHours", "workingHours": [ { "day": "Monday", "start": "09:00", "end": "17:00" } ], "enabled": true }
      ],
      "AppointmentTypes": [
        { "itemId": "consultation001", "name": "Consultation", "slug": "consultation", "durationMinutes": 30, "slotIntervalMinutes": 30,
          "resourceIds": [ "drsmith00000001" ], "assignment": "AnyAvailable", "allowSelfService": true, "enabled": true }
      ]
    }
  ]
}
```

With the **CrestApps Recipes** feature on, the module describes the `Scheduling` recipe step (`SchedulingRecipeStepSchema`) and the deployment step's settings (`SchedulingDeploymentStepSchema`) to it.

## HTTP API

The Scheduling API feature maps `/api/scheduling/v1`, authenticated with Orchard Core's `Api` scheme.

| Method and route | Does | Permission |
| --- | --- | --- |
| `GET types` | The appointment types: `id`, `name`, `description`, `durationMinutes`, `capacity`, `requiresConfirmation`, `enabled`. | View appointments |
| `GET types/{id}/slots?from=&to=&resourceId=` | Open times between two ISO 8601 times (at most 31 days): `startUtc`, `endUtc`, `resourceIds`, `remaining`. | View appointments |
| `POST appointments` | Book from an `AppointmentRequest` (`appointmentTypeId`, `startUtc`, `resourceId`, `customerName`, `customerEmail`, `customerPhone`, `userId`, `notes`, `timeZoneId`, `hold`). Returns `201`. | Manage appointments |
| `GET appointments/{id}` | One appointment. | View appointments |
| `POST appointments/{id}/confirm` | Confirm a held or requested appointment. | Manage appointments |
| `POST appointments/{id}/reschedule` | Move it: `{ "startUtc": "...", "resourceId": null }`. | Manage appointments |
| `POST appointments/{id}/cancel` | Cancel it: `{ "reason": "..." }`. | Manage appointments |

A time that was taken returns `409 Conflict`.

## AI tools

With `CrestApps.OrchardCore.AI` enabled, Scheduling registers these tools in the **Scheduling** category (an `AIToolsStartup` with `[RequireFeatures("CrestApps.OrchardCore.AI")]`). They are meant for customer-facing assistants, so they work for anonymous visitors, but only with appointment types that are enabled and allow self-service, unless the caller has **Manage appointments**.

| Function | Title | Description |
| --- | --- | --- |
| `listAppointmentTypes` | List appointment types | Lists the kinds of appointments that can be booked. |
| `findAppointmentSlots` | Find appointment slots | Finds open times for an appointment. |
| `bookAppointment` | Book appointment | Books an appointment for the customer at a `startUtc` that `findAppointmentSlots` returned. |
| `changeAppointment` | Change appointment | Cancels or moves a customer's appointment. The caller proves it is theirs with the manage code, or the email or phone number on the appointment, unless it has **Manage appointments**. |

See [AI Tools](../ai/tools.md#scheduling-tools).
