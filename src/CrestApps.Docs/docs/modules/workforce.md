---
sidebar_label: Workforce
title: Workforce
description: "Workforce management on the shared calendar: workers with or without a user account, the team hierarchy and manager scoping, activity codes, team Workforce calendars, calendar access by team, the busy time of breaks inside shifts, shift planning with templates, rotations, coverage, auto-scheduling, publishing, open shifts and shift trades, time off with accrual, balances, allotments and a waitlist, recipes and the contracts other modules build on."
user_manual:
  - user-manual/workforce/workers-and-teams
  - user-manual/workforce/shift-planning
  - user-manual/workforce/time-off
---

The **Workforce** module is the base of workforce management. It keeps the people who work and how they are organized, and gives every team a Workforce calendar on the shared [Calendars](./calendars.md) module, where shifts and approved time off are published. It uses the shared [Skills](./skills.md) catalog for workers' skills and certificates.

The admin screens are described in the User Manual: [Workers and Teams](../user-manual/workforce/workers-and-teams.md) and [Shift Planning](../user-manual/workforce/shift-planning.md) and [Time Off](../user-manual/workforce/time-off.md).

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Workforce` | Workforce | `CrestApps.OrchardCore.Calendars`, `CrestApps.OrchardCore.Skills` |
| `CrestApps.OrchardCore.Workforce.ShiftPlanning` | Workforce Shift Planning | `CrestApps.OrchardCore.Workforce` |
| `CrestApps.OrchardCore.Workforce.TimeOff` | Workforce Time Off | `CrestApps.OrchardCore.Workforce` |

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
| `Shift` | Free | Ignores capacity. The worker is named in its `ShiftData`, and is also its attendee when they have a user account or an email; a worker without either can still be scheduled. A shift is time the worker is available for work, so it doesn't block their free time; Scheduling reads Shift events for resources whose hours come from shifts. |
| `TimeOff` | Busy | All day by default, ignores capacity. Approved time off; the worker is its attendee when they have a user account or an email. |
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

## Shift planning

The **Workforce Shift Planning** feature (`CrestApps.OrchardCore.Workforce.ShiftPlanning`) plans schedules as drafts and publishes them to the team's Workforce calendar. Its services are registered by `AddWorkforceCore()`; the feature adds the index tables, the admin screens and the permissions.

| Record | What it holds |
| --- | --- |
| `ShiftTemplate` | A reusable shift: name, color, start time of day, length in minutes (up to a day, so it may end after midnight), `Segments` (activity code, offset and length in minutes), the teams that may use it (empty for all) and the skills it needs. |
| `ShiftPattern` | A rotation of a team: a length of 1 to 12 weeks, `Days` (week, weekday, template) and `Workers`, each with the date they started week 1. The week a date falls in is `((date - startDate) / 7) mod weekCount`. |
| `CoverageRequirement` | How many people a team needs: an optional skill, `Rows` (weekday or date, start and end time of day, minimum, target and optional maximum), and the days it applies. A row for a date replaces that weekday's rows of the same requirement. |
| `SchedulePeriod` | A team's schedule of 1 to 12 weeks: dates, status (`Draft`, `Published`, `Locked`), its primary scenario, and the published version, when and by whom. |
| `ScheduleScenario` | A draft of a period. Each period starts with a primary scenario, the one that is published. |
| `DraftShift` | A planned shift in a scenario: worker (or none, for an open shift), day, start and end on the team's wall clock with its time zone, template, segments, notes and a stable `ShiftKey`. |
| `ScheduleChange` | One change after the first publish: added, removed, moved, reassigned, segments changed, swapped or open shift filled, with the times before and after, the previous worker, `InitiatedBy` (`Employer` or `Worker`), the reason, the version and who made it. |
| `ScheduleSolverRun` | `ScheduleScenario.LastRun`: the solver, the coverage and minimum scores after the run (0 to 1), shifts added, runtime, whether it timed out, when and by whom. |
| `OpenShift` | A shift with nobody, offered to a team: the schedule it came from (or none for one posted on its own) and its `ShiftKey`, day, wall-clock start and end with the time zone, template, segments, required skills, notes, `ClaimPolicy` (`ManagerApproves`, `FirstCome`, `BySeniority`) and `ClaimDeadlineUtc`, `Status` (`Open`, `Claimed`, `Filled`, `Canceled`), `Claims` (worker, `Pending`, `Approved`, `Declined` or `Withdrawn`, when, who decided and a note), and the worker and calendar event it was filled with. |
| `ShiftTrade` | A worker's offer of a published shift: `Kind` (`GiveAway`, `Swap`), `Status` (`Offered`, `Accepted`, `Approved`, `Declined`, `Rejected`, `Canceled`), the offering worker and the shift's event, key and times, the colleague (or none, for anyone eligible) and, for a swap, their shift, who accepted and when, `RequiresApproval`, who decided and their note. |

| Contract | What it does |
| --- | --- |
| `IShiftTemplateManager`, `IShiftPatternManager` | Catalog managers with `ListForTeamAsync`. Validation refuses a blank name, a template outside 15 minutes to 24 hours or with segments outside the shift, and a rotation without a team, outside 1 to 12 weeks, or with a day outside its weeks. |
| `ICoverageService` | Lists, saves (validating the team, the rows and the dates) and deletes requirements. `ComputeAsync(CoverageQuery)` returns `CoverageInterval`s (minimum, target, maximum, scheduled, gap) for a team and days, in intervals of 5 to 240 minutes in the team's time zone, counting a draft scenario's shifts or, without a scenario, the published Shift events. A shift counts for an interval it covers entirely, except during a segment whose activity code doesn't count toward coverage. |
| `ISchedulePlanningService` | Creates, lists and deletes periods (only never-published ones can be deleted); saves and deletes draft shifts; `ApplyPatternsAsync` fills a scenario from the team's rotations for active workers on the team that day, skipping days that already have a shift; `CopyFromPeriodAsync` copies another period's primary scenario moved by the days between them; `PublishAsync` publishes; `ListChangesAsync` lists the change log. |
| `ISchedulePublishHandler` | Called after a publish, in the same request, with the period and the `PlanningPublishResult` (added, updated, removed, open, changes, errors). |
| `IShiftScheduleSolver` | The solver seam: `Name` and `SolveAsync(ShiftScheduleProblem)`, which returns a `ShiftScheduleProposal` (shifts as worker, day and template, coverage and minimum scores, runtime, timed out) and never writes. `ISchedulePlanningService.AutoScheduleAsync(scenarioId, AutoScheduleOptions)` builds the problem, picks the solver by name (`ShiftPlanningOptions.DefaultSolver`, `Default` for the built-in one), adds the proposed shifts to the scenario and stores the run. Another module registers its own solver with `TryAddEnumerable`. |
| `IShiftService` | Changes published shifts under a distributed lock per worker (`WORKFORCE_WORKER_{id}`, taken in id order): `ReassignAsync` and `SwapAsync`, which run the validators, update the events, keep the period's draft shift in step by `ShiftKey` and record a `ScheduleChange`; `ListWorkerShiftsAsync` across every team calendar of the worker; `CheckAsync(ShiftAssignmentContext)`. |
| `IShiftAssignmentValidator` | Checks a worker taking a shift and adds problems to the context. The built-in one checks that the worker is active and on the team that day, holds the required skills that day, has no overlapping shift, rests `ShiftPlanningOptions.MinRestHours` between shifts, and stays within `MaxHoursPerWeek` (Monday to Sunday in the team's time zone) and `MaxHoursPerDay`. Labor rule sets add their own. |
| `IOpenShiftService` | Posts, lists, checks, claims, withdraws, assigns, declines and cancels open shifts; `ListEligibleWorkersAsync`; `AwardDueAsync` gives the seniority shifts past their deadline to the most senior eligible claimant (seniority date, then hire date, then claim time). Filling creates the Shift event (`SourceId` = `ShiftKey`, `ShiftData.ScheduleId` = the period), sets the draft shift's worker and records `OpenShiftFilled`. |
| `IShiftTradeService` | Offers, checks, accepts, rejects, approves, declines and cancels trades. A shift can be in one pending trade at a time and must not have started. Accepting checks both sides with the validators and that the shifts are as offered; without approval the trade applies at once through `IShiftService`. Applying cancels the other pending trades of the same shifts. |

**Publishing.** `PublishAsync` reads the Shift events already on the team calendar whose `ShiftData.ScheduleId` is the period and matches them to draft shifts by `SourceId` = `ShiftKey`. New drafts are created as Shift events with `Source` `Workforce` and `Editable` off, changed ones are updated, and events whose draft is gone are cancelled; unchanged shifts are not touched. Draft shifts with nobody are counted, not published, and are posted as `OpenShift` records keyed by `ShiftKey`; a republish updates them, reopens one whose shift has nobody again, and cancels those whose draft is gone or now has a worker. Events are written with conflict checks and invitations off. Each published event carries `ShiftData` with its segments converted to UTC and their activity kind, which the busy time of breaks and Scheduling read. The version goes up on every publish, and from the second publish on each change is recorded as a `ScheduleChange`. A shift that the calendar refuses is reported in `Errors` and the rest are still published.

**Auto-scheduling.** The problem counts minutes from midnight of the period's first day on the team's wall clock, in intervals of `AutoScheduleOptions.IntervalMinutes` (30 by default). The demand is the team's coverage rows per skill and interval, summed across requirements; a requirement without a skill counts everyone, one with a skill counts its holders. Workers are the active members of the team during the period, with the days they are on the team, availability windows per day, the skills they hold on both the first and the last day, `MaxHoursPerWeek` (else `ContractedHoursPerWeek`, else `ShiftPlanningOptions.DefaultMaxHoursPerWeek`), `MinHoursPerWeek` and `MaxHoursPerDay`. Templates are the enabled ones the team may use, with their paid minutes and the segments that don't count toward coverage. Fixed shifts are the scenario's shifts with a worker, the members' published shifts of this team from other schedules, and their shifts on other teams, which take their time but don't count. The built-in solver builds greedily (the interval furthest below its need gets the cheapest shift covering it), then improves each shift by removing it, changing its template or giving it to another worker while that lowers the cost, within `TimeLimit` (`ShiftPlanningOptions.SolverTimeLimitSeconds`, 30) and with a repeatable `Seed`. Hard limits are one shift a day, availability, skills, `MinRestMinutes` (`MinRestHours`, 10), weekly and daily hours and `MaxConsecutiveDays` (6).

**Background task.** *Open shifts awarded by seniority* runs every five minutes and calls `AwardDueAsync`.

## Time off

The **Workforce Time Off** feature (`CrestApps.OrchardCore.Workforce.TimeOff`) registers its services with `AddWorkforceTimeOffCore()`, and adds the index tables, the admin screens, the permissions and the accrual task.

| Record | What it holds |
| --- | --- |
| `TimeOffType` | A kind of time off: name, description, color, `Unit` (`Hours`, `Days`), `HoursPerDay`, whether it is paid, needs a balance, allows part of a day, needs a reason, counts against allotments and is visible to workers, and a pay code. |
| `TimeOffPolicy` | How workers earn one type: `AccrualMethod` (`None`, `PerPeriod`, `PerHourWorked`, `AnnualLumpSum`), `Frequency` (`Weekly`, `Biweekly`, `SemiMonthly`, `Monthly`) and `AnchorDate`, `Rate`, `TenureTiers` (from months of service, rate), `WaitingPeriodDays`, `MaxBalance`, `CarryoverMax`, the reset month and day, `NegativeBalanceLimit` and `Assignments` (a team or a worker, with effective dates). |
| `TimeOffLedgerEntry` | One change to a worker's balance under a policy, only ever added: `Kind` (`Opening`, `Accrual`, `Taken`, `Reversal`, `Adjustment`, `Carryover`, `Expiry`, `Payout`), a signed `Amount`, `EffectiveDate`, `SourceRef`, `IdempotencyKey`, the actor and a note. |
| `TimeOffRequest` | A request: worker, team, type, policy, first and last day, optional start and end time for part of one day, time zone, `Days` (date, amount, minutes), `Amount`, `Status` (`Pending`, `Waitlisted`, `Approved`, `Declined`, `Canceled`, `Withdrawn`), reason, decision note, who decided and when, `AutoApproved`, who submitted and when (which orders the waitlist), the calendar event and `FlaggedShiftEventIds`. |
| `TimeOffAllotment` | How many people of a team and its sub-teams may be off: `WeekdaySlots` (Sunday first), `Dates` that override them, `TypeIds`, `AutoApprove` and `WhenFull` (`Waitlist`, `Pending`). |
| `BlackoutPeriod` | Dates no time off can be requested for: a team with its sub-teams (or every team), first and last day, `TypeIds` and a message. |

A team's `TimeOffShiftHandling` (`Flag`, `Remove`, or `null` for its parent's) says what happens to a worker's published shifts under approved time off.

| Contract | What it does |
| --- | --- |
| `ITimeOffTypeManager`, `ITimeOffAllotmentManager`, `IBlackoutPeriodManager` | Catalog managers. |
| `ITimeOffPolicyManager` | Catalog manager plus `ListForWorkerAsync(worker, date)` and `FindForWorkerAsync(worker, typeId, date)`, which pick for each type the worker's own assignment first, then the nearest team up the tree from the teams they are on that day, and `ListAssignedAsync`. |
| `ITimeOffBalanceService` | `GetBalancesAsync` and `GetBalanceAsync(worker, policy, date)` return a `TimeOffBalance`: `Current` (the ledger up to the date), `ProjectedAccrual` (what the policy owes up to the date and isn't in the ledger yet), `Upcoming` (approved time off after the date), `Pending` (open requests) and `Available`. `PageLedgerAsync` lists the ledger; `AdjustAsync(TimeOffAdjustment)` writes an opening balance, adjustment, carryover or payout, once per idempotency key. |
| `ITimeOffAccrualService` | `RunAsync(date)` and `RunForWorkerAsync(workerId, date)` write what each policy owes for the periods that ended on or before the date. |
| `IWorkedTimeSource` | The minutes a worker worked on a range of days, for accrual per hour worked. The built-in one counts the paid time of published shifts; a time clock replaces it. |
| `ITimeOffRequestService` | `PreviewAsync` (days, amount, balance, overlapping shifts, full dates, the outcome and the problems), `SubmitAsync`, `ApproveAsync`, `DeclineAsync`, `CancelAsync`, `FindAsync` and `PageAsync(TimeOffRequestQuery)`. |
| `ITimeOffApprovalHandler` | Called when time off is approved with the worker's overlapping published shifts. The built-in one cancels the shifts of teams set to `Remove`, deletes their draft shifts and records a `Removed` change initiated by the worker; the shifts left in the list are flagged on the request. |

**Accrual.** `TimeOffAccrualEngine` is pure. For a worker and a policy it owes an accrual at the end of each period (or, for a yearly amount, on the first eligible day as a share of the year and at each reset date) from the later of the assignment's start and the hire date plus the waiting period, up to the date, the assignment's end and the termination date, on the days the policy is the one that applies. The rate comes from the tenure tiers by whole months of service. An amount that would take the balance above `MaxBalance` is cut to fit and the entry notes it. At each reset date, before that day's accrual, what is above `CarryoverMax` expires. Each entry's key is `accrual:` or `expiry:` with the worker, the policy and the day, and entries whose key is in the ledger are not owed again. A run takes the worker's lock and saves before letting it go.

**Requests.** A day with published shifts counts their paid time; a weekday without counts the type's `HoursPerDay`; a weekend day without counts nothing. Part of a day counts the paid time of the shifts inside it, or its length. Submitting checks blackout dates (of the team and the teams above it), the balance on the first day against the amount less the negative limit, overlap with the worker's open and approved requests, and the reason. It runs under the worker's lock (`WORKFORCE_TIMEOFF_{id}`) and one lock per allotment the request falls under (`WORKFORCE_ALLOTMENT_{id}`), taken in key order, recounts the places under them, and saves before letting them go, so two requests for the last place can't both get it. Approved and pending requests hold a place on each day they count something. Approving creates a confirmed, busy, non-editable `TimeOff` event (`Source` `Workforce`, `SourceId` the request) on the team's calendar and a `Taken` entry dated the first day. Canceling approved time off writes a `Reversal`, cancels the event, and then, under the locks of every allotment involved, approves (or sends to a manager) the waitlisted requests of those days that now fit, in the order they were submitted, saving after each.

**Pending requests on the calendar.** `PendingTimeOffEventSource` is an `ICalendarEventSource` named `WorkforceTimeOffRequests`. For the team calendars asked for by id, it returns the open requests as tentative, free, non-editable `TimeOff` occurrences.

**Background task.** *Time off accrual* runs every hour at a quarter past and calls `RunAsync` for the site's today.

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
| `ManageShiftTemplates` (Manage shift templates) | The shift template list and editor. |
| `ManageWorkforceSchedules` (Plan team schedules) | Schedules, rotations and coverage of the teams in one's scope, without publishing. Implied by publishing. |
| `PublishWorkforceSchedules` (Publish team schedules) | Publishing the schedules of the teams in one's scope. |
| `ManageWorkforceOpenShifts` (Manage open shifts) | Posting, assigning and canceling the open shifts of the teams in one's scope, and deciding on claims. |
| `ApproveWorkforceShiftTrades` (Approve shift trades) | Approving and declining the shift trades of the teams in one's scope, and offering or accepting trades on a worker's behalf. |
| `ManageTimeOffPolicies` (Manage time-off policies) | Time-off types, policies, allotments and blackout dates, and assigning a policy to one worker. |
| `ApproveTimeOff` (Approve time off) | The time-off queue, requesting time off for a worker, approving, declining and canceling, and workers' balances, for the teams in one's scope. |
| `AdjustTimeOffBalances` (Adjust time-off balances) | Changing the balances of the workers in one's scope. |
| `RequestTimeOff` (Request time off) | Requesting one's own time off, in the self-service portal. Not implied by Manage the whole workforce. |
