# CrestApps Orchard Core Workforce

The base of workforce management on the shared calendar.

## Features

| Feature | Id | What it adds |
| --- | --- | --- |
| Workforce | `CrestApps.OrchardCore.Workforce` | Workers with or without a user account, the team hierarchy with manager scoping, activity codes, availability, workers' skills and certificates, a Workforce calendar for each team, calendar access by team, and a `Workforce` recipe and deployment step. |
| Workforce Shift Planning | `CrestApps.OrchardCore.Workforce.ShiftPlanning` | Shift templates, rotations, coverage requirements, team schedules planned on a week-by-week board and auto-scheduled, publishing to the team calendar, open shifts, shift trades and a change log. |
| Workforce Labor Rules | `CrestApps.OrchardCore.Workforce.LaborRules` | A labor rule catalog combined into rule sets that warn, block or need an override, checks on publishing, claims and trades, override records and predictability pay. |
| Workforce My Work Portal | `CrestApps.OrchardCore.Workforce.Portal` | An installable portal at `/my-work` for workers and managers, with notifications by inbox, web push and email, and a private calendar feed. |
| Workforce Time Off | `CrestApps.OrchardCore.Workforce.TimeOff` | Time-off types, policies with accrual, a balance ledger, requests and approvals, allotments that approve on their own with a waitlist, and blackout dates. |

Workforce depends on Calendars (`CrestApps.OrchardCore.Calendars`) and Skills (`CrestApps.OrchardCore.Skills`).

## Admin

- **Workforce > Workers**: the workers of the teams you manage, with their teams, hours, availability, skills and certificates.
- **Workforce > Teams**: the team tree, from organization down to crews.
- **Workforce > Activity Codes**: what workers do in each part of a shift.
- **Workforce > Schedules**, **Open Shifts**, **Shift Trades**, **Rotations**, **Coverage** and **Shift Templates** (Shift Planning): plan, auto-schedule and publish team schedules, fill open shifts and approve trades.
- **Workforce > Time Off**, **Time-Off Types**, **Time-Off Policies**, **Time-Off Allotments** and **Blackout Dates** (Time Off): decide on requests and set up how time off is earned and limited. A worker's balances and ledger are under **Time off** in their row's menu.
- **/my-work** (My Work Portal): the workers' own portal, and approvals and who's working for managers.
- **Workforce > Labor Rules**, **Rule Overrides** and **Predictability Pay** (Labor Rules): set up rule sets, see the rules gone ahead with, and approve the premium owed for late changes.

## Extending

Inject `IWorkerManager`, `ITeamManager` and `IActivityCodeManager` for the records, and `IWorkforceScopeService` to limit what a user sees to the teams they manage. Published shifts are `Shift` events on a team's Workforce calendar with `ShiftData` in their properties; the breaks inside them are busy time through `ShiftSegmentBusyTimeSource`. `ISchedulePlanningService` and `ICoverageService` plan and publish schedules, and `ISchedulePublishHandler` hears about each publish. `IShiftScheduleSolver` is the auto-scheduler seam, `IOpenShiftService` and `IShiftTradeService` run open shifts and trades, `IShiftService` changes published shifts, and `IShiftAssignmentValidator` checks who may take a shift. `ITimeOffRequestService`, `ITimeOffBalanceService` and `ITimeOffAccrualService` run time off; `ITimeOffApprovalHandler` hears about approved time off and `IWorkedTimeSource` says how long a worker worked. Register a labor rule with `services.AddLaborRule<T>()`; `ILaborRuleEvaluator` checks shifts, `ISchedulePublishValidator` checks a schedule before publishing, `IScheduleChangeHandler` hears about each recorded change and `IWorkerScheduleLimitsProvider` gives a worker's limits. `IWorkforceNotifier` tells workers about changes through every `IWorkforceNotificationChannel`; add a channel to reach them another way.

## Documentation

- User Manual: [Workers and Teams](../../CrestApps.Docs/docs/user-manual/workforce/workers-and-teams.md), [Shift Planning](../../CrestApps.Docs/docs/user-manual/workforce/shift-planning.md), [Time Off](../../CrestApps.Docs/docs/user-manual/workforce/time-off.md), [Labor Rules](../../CrestApps.Docs/docs/user-manual/workforce/labor-rules.md), [My Work Portal](../../CrestApps.Docs/docs/user-manual/workforce/my-work-portal.md)
- Technical Manual: [Workforce](../../CrestApps.Docs/docs/modules/workforce.md)
