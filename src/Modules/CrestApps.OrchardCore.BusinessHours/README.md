# Business Hours

Business hours schedules that any module can use: weekly hours with lunch breaks, holidays and days with special hours, in a time zone.

## Features

| Feature | Id | What it does |
| --- | --- | --- |
| Business Hours | `CrestApps.OrchardCore.BusinessHours` | The **Configuration > Business Hours** list and editor, the schedule picker, the `BusinessHoursSchedules` recipe and deployment steps, and the **Check Business Hours** workflow task |

## How it fits

The Business Hours building block has three layers:

- `CrestApps.OrchardCore.BusinessHours.Abstractions`: `HoursSchedule` (time zone, weekly windows, date exceptions), `IOpenHoursService` (is it open, open windows, add open time, open time between two instants), `IHoursScheduleSource`, the pure `HoursScheduleEvaluator`, and the `BusinessHoursSchedule` record this module keeps.
- `CrestApps.OrchardCore.BusinessHours.Core`: `DefaultOpenHoursService`, `AddOpenHours()`, `AddBusinessHoursSchedules()`, the schedule rules and the editor helpers (`WeeklyHoursRows`, `HoursDateExceptionText`).
- This module: the admin pages, the picker, the recipe and deployment steps and the workflow task.

Every schedule kept here is an `IHoursScheduleSource`, so the open-hours service answers for it. Other sources keep working beside it: the Contact Center business hours calendars are one.

## Using it from another module

- Ask "are we open?" through `IOpenHoursService` (reference `BusinessHours.Abstractions`, call `services.AddOpenHours()` from `BusinessHours.Core`). A schedule nobody knows, or a disabled one, is unknown: `IsOpenAsync` returns `null` and the time arithmetic counts around the clock.
- Let users pick a schedule with the `HoursSchedulePicker` view component, by name:

  ```cshtml
  @await Component.InvokeAsync("HoursSchedulePicker", new { name = "MyModel.ScheduleId", selectedValue = Model.ScheduleId, emptyText = T["Around the clock"].Value })
  ```

  Declare a dependency on the Business Hours feature so the picker is there.
- Keep your own schedules by implementing `IHoursScheduleSource` and registering it with `services.AddHoursScheduleSource<T>()`.

## Recipe step

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
          { "Day": 1, "OpenMinute": 540, "CloseMinute": 1020 },
          { "Day": 2, "OpenMinute": 540, "CloseMinute": 1020 }
        ],
        "DateExceptions": [
          { "Date": "2026-12-25", "Name": "Christmas Day", "Closed": true }
        ]
      }
    }
  ]
}
```

A schedule is matched by `ItemId`, then by `Name`. `Day` is 0 for Sunday to 6 for Saturday; minutes count from local midnight.

## Permissions

`ManageBusinessHoursSchedules` lets a user list, create, edit and delete schedules. Administrators have it by default.

## Documentation

- User manual: `src/CrestApps.Docs/docs/user-manual/configuration/business-hours.md`
- Technical manual: `src/CrestApps.Docs/docs/modules/business-hours.md`
