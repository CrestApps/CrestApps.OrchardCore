---
sidebar_label: Business Hours
title: Business Hours
description: "The shared business hours building block: the HoursSchedule model with weekly windows and date exceptions (holidays and special hours), the IOpenHoursService every module asks whether something is open, schedule sources, and the pure HoursScheduleEvaluator."
---

**Business Hours** is the one place a module asks "are we open?". Queues, entry points and the dialer, automated SMS sends, the Omnichannel load editor and work-order service levels all read hours through it, so a holiday entered once closes every one of them.

It has no feature of its own. It is two libraries:

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.BusinessHours.Abstractions` | `HoursSchedule`, `HoursWindow`, `HoursDateException`, `HoursScheduleOption`, `IOpenHoursService`, `IHoursScheduleSource`, `AddHoursScheduleSource<T>()` and the pure `HoursScheduleEvaluator` |
| `CrestApps.OrchardCore.BusinessHours.Core` | `DefaultOpenHoursService` and `AddOpenHours()` |

The schedules themselves are kept by the features that own them. Today that is the Contact Center **Business Hours** feature (`CrestApps.OrchardCore.ContactCenter.BusinessHours`), whose business-hours calendars are a schedule source. See [Contact Center configuration](../contact-center/configuration-deployment.md).

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
| Work-order service levels | `OpenHoursServiceHoursProvider`, an `IServiceHoursProvider` registered by the Work Orders module; a policy's weekly hours are evaluated by `HoursScheduleEvaluator` too |

The SMS automation feature no longer depends on the Contact Center Business Hours feature: it uses the gate that Omnichannel Activities registers, and the calendars appear in its pickers when the Business Hours feature is enabled.
