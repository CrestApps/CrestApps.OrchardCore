# CrestApps Orchard Core Workforce

The base of workforce management on the shared calendar.

## Features

| Feature | Id | What it adds |
| --- | --- | --- |
| Workforce | `CrestApps.OrchardCore.Workforce` | Workers with or without a user account, the team hierarchy with manager scoping, activity codes, availability, workers' skills and certificates, a Workforce calendar for each team, calendar access by team, and a `Workforce` recipe and deployment step. |

The feature depends on Calendars (`CrestApps.OrchardCore.Calendars`) and Skills (`CrestApps.OrchardCore.Skills`).

## Admin

- **Workforce > Workers**: the workers of the teams you manage, with their teams, hours, availability, skills and certificates.
- **Workforce > Teams**: the team tree, from organization down to crews.
- **Workforce > Activity Codes**: what workers do in each part of a shift.

## Extending

Inject `IWorkerManager`, `ITeamManager` and `IActivityCodeManager` for the records, and `IWorkforceScopeService` to limit what a user sees to the teams they manage. Published shifts are `Shift` events on a team's Workforce calendar with `ShiftData` in their properties; the breaks inside them are busy time through `ShiftSegmentBusyTimeSource`.

## Documentation

- User Manual: [Workers and Teams](../../CrestApps.Docs/docs/user-manual/workforce/workers-and-teams.md)
- Technical Manual: [Workforce](../../CrestApps.Docs/docs/modules/workforce.md)
