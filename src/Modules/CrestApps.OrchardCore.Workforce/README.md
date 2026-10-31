# CrestApps Orchard Core Workforce

The base of workforce management on the shared calendar.

## Features

| Feature | Id | What it adds |
| --- | --- | --- |
| Workforce | `CrestApps.OrchardCore.Workforce` | Workers with or without a user account, the team hierarchy with manager scoping, activity codes, availability, workers' skills and certificates, a Workforce calendar for each team, calendar access by team, and a `Workforce` recipe and deployment step. |
| Workforce Shift Planning | `CrestApps.OrchardCore.Workforce.ShiftPlanning` | Shift templates, rotations, coverage requirements, team schedules planned on a week-by-week board, publishing to the team calendar and a change log. |

Workforce depends on Calendars (`CrestApps.OrchardCore.Calendars`) and Skills (`CrestApps.OrchardCore.Skills`).

## Admin

- **Workforce > Workers**: the workers of the teams you manage, with their teams, hours, availability, skills and certificates.
- **Workforce > Teams**: the team tree, from organization down to crews.
- **Workforce > Activity Codes**: what workers do in each part of a shift.
- **Workforce > Schedules**, **Rotations**, **Coverage** and **Shift Templates** (Shift Planning): plan and publish team schedules.

## Extending

Inject `IWorkerManager`, `ITeamManager` and `IActivityCodeManager` for the records, and `IWorkforceScopeService` to limit what a user sees to the teams they manage. Published shifts are `Shift` events on a team's Workforce calendar with `ShiftData` in their properties; the breaks inside them are busy time through `ShiftSegmentBusyTimeSource`. `ISchedulePlanningService` and `ICoverageService` plan and publish schedules, and `ISchedulePublishHandler` hears about each publish.

## Documentation

- User Manual: [Workers and Teams](../../CrestApps.Docs/docs/user-manual/workforce/workers-and-teams.md), [Shift Planning](../../CrestApps.Docs/docs/user-manual/workforce/shift-planning.md)
- Technical Manual: [Workforce](../../CrestApps.Docs/docs/modules/workforce.md)
