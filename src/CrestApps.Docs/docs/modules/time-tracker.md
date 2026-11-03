---
sidebar_label: Time Tracker
title: Time Tracker
description: "Time tracking for any user: stages you set up, the categories their time counts toward, clocking in and switching stage, manager edits kept with a reason and in the Audit Trail, and a report with a column per category."
user_manual:
  - user-manual/workforce/time-tracker
---

The **Time Tracker** module (`CrestApps.OrchardCore.TimeTracker`, with `TimeTracker.Abstractions` and `TimeTracker.Core`) records how people spend their working time. A person is always in at most one **stage**, such as Work, Break or Lunch; each stage counts toward any number of **categories**, such as Payable, Billable, Break or Vacation, and the report totals each category. It needs only Users, so it works on sites without workforce planning.

The admin screens are described in the User Manual: [Time Tracker](../user-manual/workforce/time-tracker.md).

## Features

| Feature | Id | Depends on |
| --- | --- | --- |
| Time Tracker | `CrestApps.OrchardCore.TimeTracker` | Users, CrestApps Users |

It works with other features when they are on:

| Feature on | What it adds |
| --- | --- |
| Audit Trail | Every recorded, ended, changed and removed entry, with who and why. |
| Recipes, Deployment | The `TimeTracker` recipe step and deployment step. |
| CrestApps Recipes | The steps' schemas (`IRecipeStep`, deployment step schema). |

## Records

All live in the `TimeTracker` YesSql collection, each with its own index table.

- **`TimeCategory`**: `Name` (unique), `Code`, `Color`, `Position` (the report column order), `Factor` (how a minute counts: 1 as it is, 1.5 half again, such as an overtime premium) and `Enabled`.
- **`TimeStage`**: `Name` (unique), `Code` (for exports and mapping Contact Center states), `Color`, `Position`, `CategoryIds`, `SelfService` (people pick it on their clock; off for stages that come only from integrations or managers) and `Enabled`.
- **`TimeEntry`**: `UserId` and `UserName`, `StageId`, `StartUtc`, `EndUtc` (none while the person is in it), `Source` (`Self`, `Manager`, `Kiosk`, `Mobile`, `ContactCenter`, `TimeOff`, `Api`, `Import`) with `SourceRef`, `Note`, `ManagedBy` (the clock that keeps it, if another one does), `CreatedBy` and `Changes` (each with when, who, why and what it was before).

`TimeEntryIndex` keeps the person, stage, start, end (the far future while open), whether it is open, and the source.

## Behavior

`ITimeTrackerService` does every change, under a distributed lock per person (`TIMETRACKER_USER_{id}`), saving before the lock is let go.

- **`SwitchAsync`** puts a person in a stage from now: it clocks them in, or ends the stage they are in at this second and starts the new one, so the day has no gaps or overlaps. Switching to the stage they are in changes nothing.
- **`ClockOutAsync`** ends the stage they are in.
- **`AddAsync`** records an entry that has ended (a manager's, an import's, approved time off). **`UpdateAsync`** changes a stage, times or note and needs a reason, kept in `Changes`. **`DeleteAsync`** needs a reason too. None of them lets a person's entries overlap or time be recorded in the future.
- **`GetTotalsAsync`** totals a range for each person, or each person and day in a time zone (an entry over midnight is split): minutes by stage, minutes by category after its factor, and every tracked minute. An open entry counts up to now.
- `ITimeEntryObserver` hears about every change after it is saved; the Audit Trail is one.

## Other clocks

Another clock, such as the Workforce time clock, can keep a person's time and show it here:

- **`SyncAsync(TimeEntrySync)`** replaces the entries a clock keeps (`ManagedBy`) for a person over a range with the ones it builds, which it builds under the person's lock so two syncs never write stale time. Entries that didn't change keep their ids. Time kept here, such as a manager's entry, stays, and the clock's entries are cut around it; an entry left open here ends where the clock's next time starts. With `ReplacesRangeOnly`, only the range is rebuilt, for a clock that reads the end of a growing log: its entry running into the range ends at the range's start, or goes on when the range starts in the same stage. `Overrides` names clocks whose entries are cut around this clock's instead, such as the time clock's work around contact center states.
- **`IExternalTimeClock`** says which people a clock keeps (`FindAsync`) and names a clock by its id (`FindByIdAsync`), with where to open it. For those people, `SwitchAsync` is refused and My Time links to the clock. An entry a clock keeps can't be changed or removed here (`UpdateAsync` and `DeleteAsync` are refused); it is corrected in the clock, which syncs it again. Clocking out of an entry started here stays possible.
- `ITimeStageManager` and `ITimeCategoryManager` let other modules set up stages and categories.

## Recipe step

```json
{
  "name": "TimeTracker",
  "Categories": [ { "name": "Payable", "code": "PAY" }, { "name": "Billable" } ],
  "Stages": [ { "name": "Work", "code": "WORK", "categories": [ "Payable", "Billable" ] } ],
  "Entries": [ { "userName": "ana", "stage": "Work", "startUtc": "2026-11-02T14:00:00Z", "endUtc": "2026-11-02T22:00:00Z" } ]
}
```

Categories and stages are matched by name and updated; an entry is skipped when the person already has time then. The deployment step exports the categories and stages and, when asked, the entries of the last days.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageTimeTrackerSettings` (Manage time stages and categories) | Stages and categories. |
| `ManageTimeEntries` (Edit anyone's time) | Adding, changing and removing anyone's time. |
| `ViewTimeEntries` (View anyone's time and the time report) | The entries list and the report. |
| `TrackOwnTime` (Track one's own time) | My Time: the clock, today and the week. |

Each permission is implied by the one above it.
