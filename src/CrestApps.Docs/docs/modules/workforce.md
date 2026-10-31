---
sidebar_label: Workforce
title: Workforce
description: "Workforce management on the shared calendar: workers with or without a user account, the team hierarchy and manager scoping, activity codes, team Workforce calendars, calendar access by team, the busy time of breaks inside shifts, recipes and the contracts other modules build on."
user_manual:
  - user-manual/workforce/workers-and-teams
---

The **Workforce** module is the base of workforce management. It keeps the people who work and how they are organized, and gives every team a Workforce calendar on the shared [Calendars](./calendars.md) module, where shifts and approved time off are published. It uses the shared [Skills](./skills.md) catalog for workers' skills and certificates.

The admin screens are described in the User Manual: [Workers and Teams](../user-manual/workforce/workers-and-teams.md).

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Workforce` | Workforce | `CrestApps.OrchardCore.Calendars`, `CrestApps.OrchardCore.Skills` |

Projects: `CrestApps.OrchardCore.Workforce.Abstractions` (models and contracts), `CrestApps.OrchardCore.Workforce.Core` (stores, indexes, services), `CrestApps.OrchardCore.Workforce` (the module).

## Model

| Record | What it holds |
| --- | --- |
| `WorkerProfile` | A person who works: name, an optional `UserId`, contact details, employee number (unique), HR id, employment type and status, hire, seniority and termination dates, date of birth (sensitive), line manager, `TeamMemberships` (team, role `Member`/`Lead`/`Scheduler`/`Manager`, effective dates), time zone, home base, contracted, fewest and most hours, and weekly `Availability` windows (`Available`, `Preferred`, `Unavailable`). Other modules add their data to its `Properties` bag. |
| `WorkforceTeam` | A part of the organization: name, kind (`Organization`, `Region`, `Site`, `Department`, `Team`, `Crew`), parent, description, linked role, time zone, location, its calendar id, and the crew lead for a crew. The hierarchy can't contain a loop. |
| `ActivityCode` | What a worker does during a segment of a shift: name, short code, kind, color, and whether it is paid, counts as work and counts toward coverage. The usual nine codes are seeded by the module's migration. |
| `ShiftData` and `ShiftSegment` | What Workforce keeps on a published Shift event, in its `Properties`: the worker, team, schedule, template, a stable shift key, the published version, and the segments (activity, kind, start and end in UTC). |

Every record is a YesSql document in the `Workforce` collection, with `WorkerIndex`, `WorkerTeamMembershipIndex` (one row per membership, with its dates as `yyyyMMdd` numbers), `TeamIndex` and `ActivityCodeIndex`.

## The Workforce calendar type

The module registers the `Workforce` calendar type with `ManagedByCode()` and creates one calendar per team when the team is created (`Source` = `Workforce`, `SourceId` = the team id). The calendar follows the team's name, time zone and whether it is in use.

| Event type | Busy or free | Notes |
| --- | --- | --- |
| `Shift` | Free | Requires an attendee and ignores capacity. A shift is time the worker is available for work, so it doesn't block their free time; Scheduling reads Shift events for resources whose hours come from shifts. |
| `TimeOff` | Busy | All day by default, requires an attendee. |
| `Training`, `Meeting` | Busy | |
| `OnCall` | Free | Requires an attendee. |

`ShiftSegmentBusyTimeSource` (an `ICalendarBusyTimeSource`) reports the break, meal, meeting, training, coaching, offline work, travel and time off segments of a shift as busy for its attendees. Free/busy, the team availability page and Scheduling therefore offer a staff member during their shifts but not during those segments.

## Services

| Contract | What it does |
| --- | --- |
| `IWorkerManager` | `ICatalogManager<WorkerProfile>` plus `FindByUserIdAsync`, `FindByEmployeeNumberAsync`, `SearchAsync(WorkerQuery, skip, take)` (search, status, a team with its sub-teams, and a set of teams such as a manager's scope) and `ListByTeamAsync(teamId, date)` (members that day). Validation refuses a blank name, a duplicate employee number or user, an unknown team, a bad time zone, a membership that ends before it starts, and fewest hours above most hours. |
| `ITeamManager` | `ICatalogManager<WorkforceTeam>` plus `ListTreeAsync` (each team followed by its sub-teams, with its depth), `GetSubtreeIdsAsync` and `GetAncestorsAsync`. |
| `IActivityCodeManager` | `ICatalogManager<ActivityCode>` plus `GetEnabledAsync`. |
| `IWorkforceScopeService` | `GetManagedScopeAsync(user)` returns `WorkforceScope.Everything` for **Manage the whole workforce**, otherwise the teams where the user's worker is a `Manager` or `Scheduler` today, with their sub-teams. `ManagesWorkerAsync(user, worker)` checks that a worker belongs, today, to a team in that scope. The scope is worked out once per request. |

## Calendar access by team

`WorkforceCalendarAccessContributor` is an `ICalendarAccessContributor`, a seam of the Calendars module that lets a module grant access to calendars for reasons the calendar doesn't record. On a team's Workforce calendar:

- users whose scope covers the team (its managers and schedulers, and those of the teams above it, or anyone with **Manage the whole workforce**) get **Manage**;
- the team's members today, and the users in its linked role, get **Read**.

The calendars it grants appear in each user's calendar list.

## Recipes and deployment

The `Workforce` step imports activity codes (by id or name), teams (by id or name, with the parent by `parentName` in any order) and workers (by id, employee number or user, with each membership's team by `teamName`). Every record goes through its manager, so it is validated and each new team gets its calendar.

```json
{
  "steps": [
    {
      "name": "Workforce",
      "ActivityCodes": [
        { "name": "Break", "code": "BRK", "kind": "Break", "isPaid": true, "countsTowardCoverage": false }
      ],
      "Teams": [
        { "name": "East", "kind": "Region" },
        { "name": "Boston", "kind": "Site", "parentName": "East", "timeZoneId": "America/New_York" }
      ],
      "Workers": [
        { "displayName": "Ana Lopez", "employeeNumber": "E100", "employmentType": "PartTime",
          "teamMemberships": [ { "teamName": "Boston", "role": "Manager" } ] }
      ]
    }
  ]
}
```

The **Workforce teams and workers** deployment step exports the activity codes and teams, and optionally the workers, without their date of birth.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageAllWorkforce` (Manage the whole workforce) | Every team and worker, regardless of who manages them. Implies the others. |
| `ManageWorkforceTeams` (Manage workforce teams) | The team list and editor. |
| `ManageWorkers` (Manage workers) | Adding, changing and removing the workers in one's scope. |
| `ViewWorkers` (View workers) | The worker list, limited to one's scope. |
| `ViewSensitiveWorkerData` (View sensitive worker details) | Seeing and changing a worker's date of birth. |
| `ManageActivityCodes` (Manage activity codes) | The activity code list and editor. |
