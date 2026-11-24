---
sidebar_label: Business Hours
title: Business Hours
description: "The shared business hours building block: the HoursSchedule model with weekly windows and date exceptions (holidays and special hours), the IOpenHoursService every module asks whether something is open, schedule sources, the Business Hours feature with its schedules, picker, recipe step and workflow task, and the pure HoursScheduleEvaluator."
user_manual: user-manual/administration/business-hours-schedules
---

**Business Hours** is the one place a module asks "are we open?". Queues, entry points and the dialer, automated SMS sends, the Omnichannel load editor and work-order service levels all read hours through it, so a holiday entered once closes every one of them.

It is three projects:

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.BusinessHours.Abstractions` | `HoursSchedule`, `HoursWindow`, `HoursDateException`, `HoursScheduleOption`, `IOpenHoursService`, `IHoursScheduleSource`, `AddHoursScheduleSource<T>()`, the pure `HoursScheduleEvaluator`, the `BusinessHoursSchedule` record, `BusinessHoursConstants` and `BusinessHoursPermissions` |
| `CrestApps.OrchardCore.BusinessHours.Core` | `DefaultOpenHoursService`, `AddOpenHours()`, `AddBusinessHoursSchedules()`, the schedule rules (`BusinessHoursScheduleHandler`) and source (`BusinessHoursScheduleSource`), and the editor helpers `WeeklyHoursRows` and `HoursDateExceptionText` |
| `CrestApps.OrchardCore.BusinessHours` (module) | The **Business Hours** feature: the admin pages, the `HoursSchedulePicker` view component, the recipe and deployment steps and the **Check Business Hours** workflow task |

Schedules come from every registered source. The Business Hours feature keeps the schedules every module uses: the Contact Center **Business Hours** feature (`CrestApps.OrchardCore.ContactCenter.BusinessHours`) depends on it, and a queue's, entry point's, dialer profile's or activity load's calendar id is a schedule id. Before version 3.0 the Contact Center kept its own calendars; an upgrade copies each one to a schedule with the same id. See [Contact Center configuration](../contact-center/configuration-deployment.md).

## The Business Hours feature

| Feature | Id | Depends on |
| --- | --- | --- |
| Business Hours | `CrestApps.OrchardCore.BusinessHours` | nothing |

It adds **Content** → **Business Hours**, a list of schedules (on the shared admin list, so the List and Grid layouts and the Buttons and Menu row actions apply) and an editor with three cards: **General** (name, description, time zone, enabled), **Weekly hours** (each day open or closed, with up to two windows so a lunch break can be entered) and **Holidays and special days** (one date per line: `2026-12-25 Christmas Day` is closed all day, `2026-12-24 09:00-13:00 Christmas Eve` has special hours). The editor is built through `IDisplayManager<BusinessHoursSchedule>`, so another module adds a card with its own display driver.

A disabled schedule is unknown to `IOpenHoursService`: `IsOpenAsync` returns `null` and the time arithmetic counts around the clock. A new schedule takes the site's time zone.

A module that counts time on schedules implements `IBusinessHoursScheduleObserver`: `ChangingAsync(scheduleId)` runs before a schedule is changed or deleted, while reads still return its old hours, and `ChangedAsync(scheduleId)` after; the new hours are readable once the change commits. Service Levels uses it to settle and reschedule its running timers.

The permission is `ManageBusinessHoursSchedules` (**Manage business hours schedules**); administrators have it by default.

### The schedule picker

Any module that depends on the feature can let users pick a schedule from every source:

```cshtml
@await Component.InvokeAsync("HoursSchedulePicker", new
{
    name = Html.NameFor(model => model.HoursScheduleId).ToString(),
    selectedValue = Model.HoursScheduleId,
    emptyText = T["Around the clock"].Value,
})
```

`required` hides the empty choice. A selected schedule that was disabled or deleted stays listed as "(no longer available)", so saving the form doesn't drop it silently.

### Recipe and deployment step

The `BusinessHoursSchedules` step creates or updates schedules, matched by `ItemId` and then by `Name`:

```json
{
  "name": "BusinessHoursSchedules",
  "Schedules": [
    {
      "Name": "Support desk",
      "Enabled": true,
      "Hours": {
        "TimeZoneId": "America/New_York",
        "Windows": [
          { "Day": 1, "OpenMinute": 540, "CloseMinute": 720 },
          { "Day": 1, "OpenMinute": 780, "CloseMinute": 1020 }
        ],
        "DateExceptions": [
          { "Date": "2026-12-25", "Name": "Christmas Day", "Closed": true },
          { "Date": "2026-12-24", "Name": "Christmas Eve", "Closed": false, "OpenMinute": 540, "CloseMinute": 780 }
        ]
      }
    }
  ]
}
```

`Day` is 0 for Sunday to 6 for Saturday, and minutes count from local midnight. The **Business hours schedules** deployment step exports every schedule in the same shape. Both have schemas for CrestApps Recipes.

### Check Business Hours workflow task

The **Check Business Hours** task (`CheckBusinessHoursTask`, category **Business Hours**) checks whether a schedule is open now and takes the **Open**, **Closed** or **Unknown** outcome. **Unknown** means the schedule is disabled or no source knows it. An optional **Time zone** (a Liquid expression) reads the schedule in another zone, such as the contact's.

## The schedule

```csharp
public sealed class HoursSchedule
{
    public string TimeZoneId { get; set; }                       // IANA or Windows id; empty means UTC
    public IList<HoursWindow> Windows { get; set; }              // { Day, OpenMinute, CloseMinute }
    public IList<HoursDateException> DateExceptions { get; set; } // { Date, Name, Closed, OpenMinute, CloseMinute }
}
```

- A day can have several windows (a lunch break) or none (closed).
- A window that opens and closes at the same minute is open all day.
- A window that closes before it opens runs past midnight: a Friday window from 22:00 to 06:00 is open Friday night and early Saturday.
- A date exception owns its whole local date. `Closed = true` (the default) is a holiday; otherwise the date is open only from `OpenMinute` to `CloseMinute`. The early hours of an overnight window that run into a holiday are closed.
- Wall-clock times convert with `CalendarTime`: a time a daylight-saving change skips moves forward by the gap, and a time that happens twice is the earlier of the two.

## Asking whether something is open

Inject `IOpenHoursService`:

| Method | Returns |
| --- | --- |
| `IsOpenAsync(scheduleId, utcInstant, timeZoneId?)` | `true`/`false`, or `null` when the id is empty or no source knows the schedule (or it is disabled). Treat `null` as open, so a missing schedule never blocks live work. |
| `GetOpenWindowsAsync(scheduleId, fromUtc, toUtc, timeZoneId?)` | The open `TimeWindow`s, merged and clipped to the range, or `null` for an unknown schedule. |
| `ListSchedulesAsync()` | Every source's schedules, each once, by name, for an editor picker. |
| `AddOpenTimeAsync(scheduleId, fromUtc, duration, timeZoneId?)` | The instant at which the schedule has been open for `duration`: "eight business hours from Friday 4 pm". An unknown schedule counts around the clock. |
| `GetOpenTimeBetweenAsync(scheduleId, fromUtc, toUtc, timeZoneId?)` | How long the schedule is open between two instants. An unknown schedule counts around the clock. |

`timeZoneId` reads the schedule in another zone than its own, such as the contact's local time.

A feature that asks registers the service with `services.AddOpenHours()` (it registers once, whichever feature asks first).

## Keeping schedules

A feature that stores schedules implements `IHoursScheduleSource` (`FindAsync(id)` and `ListAsync()`) and registers it:

```csharp
services
    .AddOpenHours()
    .AddHoursScheduleSource<MyHoursScheduleSource>();
```

The first source that knows an id answers for it. A module that holds a schedule in memory can evaluate it without the service through `HoursScheduleEvaluator.IsOpen` and `HoursScheduleEvaluator.GetOpenWindows`.

## Consumers

| Consumer | How it reads hours |
| --- | --- |
| Contact Center queues, entry points, dialer and manual screening | `IBusinessHoursService`, which answers through `IOpenHoursService` |
| Omnichannel automated sends and the load editor | `IBusinessHoursGate` (`OpenHoursBusinessHoursGate`), registered by Omnichannel Activities; always open when no source knows the calendar |
| Service levels (work orders and every other scope) | The shared Service Levels block counts each timer on its policy's schedule through `AddOpenTimeAsync` and `GetOpenTimeBetweenAsync`; see [Service Levels](service-levels.md) |

The SMS automation feature no longer depends on the Contact Center Business Hours feature: it uses the gate that Omnichannel Activities registers, and the calendars appear in its pickers when the Business Hours feature is enabled.
