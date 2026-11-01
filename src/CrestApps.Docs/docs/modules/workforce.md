---
sidebar_label: Workforce
title: Workforce
description: "Workforce management on the shared calendar: workers with or without a user account, the team hierarchy and manager scoping, activity codes, team Workforce calendars, calendar access by team, the busy time of breaks inside shifts, shift planning with templates, rotations, coverage, auto-scheduling, publishing, open shifts and shift trades, time off with accrual, balances, allotments and a waitlist, labor rules with overrides and predictability pay, the My Work portal with web push, a time clock with timesheets and payroll export, recipes and the contracts other modules build on."
user_manual:
  - user-manual/workforce/workers-and-teams
  - user-manual/workforce/shift-planning
  - user-manual/workforce/time-off
  - user-manual/workforce/labor-rules
  - user-manual/workforce/time-clock
  - user-manual/workforce/reports-and-automation
  - user-manual/workforce/my-work-portal
---

The **Workforce** module is the base of workforce management. It keeps the people who work and how they are organized, and gives every team a Workforce calendar on the shared [Calendars](./calendars.md) module, where shifts and approved time off are published. It uses the shared [Skills](./skills.md) catalog for workers' skills and certificates.

The admin screens are described in the User Manual: [Workers and Teams](../user-manual/workforce/workers-and-teams.md) and [Shift Planning](../user-manual/workforce/shift-planning.md), [Time Off](../user-manual/workforce/time-off.md), [Labor Rules](../user-manual/workforce/labor-rules.md), [Time Clock](../user-manual/workforce/time-clock.md) and [My Work Portal](../user-manual/workforce/my-work-portal.md).

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Workforce` | Workforce | `CrestApps.OrchardCore.Calendars`, `CrestApps.OrchardCore.Skills` |
| `CrestApps.OrchardCore.Workforce.ShiftPlanning` | Workforce Shift Planning | `CrestApps.OrchardCore.Workforce` |
| `CrestApps.OrchardCore.Workforce.TimeOff` | Workforce Time Off | `CrestApps.OrchardCore.Workforce` |
| `CrestApps.OrchardCore.Workforce.LaborRules` | Workforce Labor Rules | `CrestApps.OrchardCore.Workforce.ShiftPlanning` |
| `CrestApps.OrchardCore.Workforce.TimeClock` | Workforce Time Clock | `CrestApps.OrchardCore.Workforce` |
| `CrestApps.OrchardCore.Workforce.Api` | Workforce API | `CrestApps.OrchardCore.Workforce` |
| `CrestApps.OrchardCore.Workforce.Portal` | Workforce My Work Portal | `CrestApps.OrchardCore.Workforce` |

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

## Labor rules

The **Workforce Labor Rules** feature (`CrestApps.OrchardCore.Workforce.LaborRules`) registers its services with `AddWorkforceLaborRulesCore()`, and adds the index tables, the admin screens, the permissions and the `WorkforceLaborRules` recipe step.

**The catalog.** A rule is an `ILaborRule` registered with `services.AddLaborRule<TRule>()`: a `Key`, a `DisplayName` and `Description`, typed `Parameters` (`LaborRuleParameter`: name, label, `LaborRuleParameterKind` and a default written invariantly), `RecordsOnly` for a rule with no enforcement, and `EvaluateAsync(LaborRuleEvaluationContext, LaborRuleInstance)`, which calls `context.Report(...)` for each violation. The context holds the worker, the team, its time zone, the rule set, now, the worker's shifts in start order (`IsCandidate` for the ones being checked; each with its day, UTC times, paid and break minutes and required skills), the changes being made (`LaborRuleChange`) and the worker's approved time off. The built-in rules are listed in `LaborRuleKeys`: `MaxHoursPerDay`, `MaxHoursPerWeek`, `MinHoursPerWeek`, `MinRest`, `MaxConsecutiveDays`, `DaysOffPerWeek`, `MaxShiftLength`, `MinShiftLength`, `Breaks`, `MinorLimits`, `SkillsRequired`, `NoWorkDuringTimeOff`, `ScheduleNotice`, `Overtime` and `PredictabilityPay` (records only).

| Record | What it holds |
| --- | --- |
| `LaborRuleSet` | Name, description, jurisdiction, `Rules` (`LaborRuleInstance`: id, rule key, `Enforcement` (`Warn`, `Block`, `RequireOverride`) and `Parameters` by name), `TeamIds`, `WorkerIds`, `EmploymentTypes` (empty for all) and whether it is in use. |
| `LaborRuleOverride` | A violation gone ahead with: worker, team, rule set, rule, message, reason, source (`Publish`), schedule, shifts, actor and time. |
| `PredictabilityPayEntry` | Worker, team, schedule, the `ScheduleChange`, shift, `ChangeKind` (`Added`, `Moved`, `Removed`), the shift's start, the notice in hours, the premium `Hours`, the rule set, `Status` (`Pending`, `Approved`, `Waived`, `Exported`), a note, who decided and when. |

| Contract | What it does |
| --- | --- |
| `ILaborRuleSetManager` | Catalog manager plus `ListRules()` and `NewRule(key)` with the rule's default parameters. Validation refuses a rule not in the catalog and fills missing parameters with their defaults. |
| `ILaborRuleEvaluator` | `ResolveRuleSetAsync(worker, teamId)`: the enabled set for the worker's employment type assigned to the worker, or else the nearest team's from the team up. `EvaluateAsync(LaborRuleRequest)` adds the worker's published shifts eight days either side (less `IgnoredShiftKeys`) and approved time off to the candidates, runs each rule of the set and returns a `LaborRuleResult` (`Violations`, `IsBlocked`, `NeedsOverride`). |
| `ILaborRuleOverrideService` | Records and pages overrides. |
| `IPredictabilityPayService` | Pages entries, approves and waives (with a reason) pending ones. |

**Where rules are enforced.**

- **Publishing.** Shift Planning calls every `ISchedulePublishValidator` before writing anything, with the drafts, their workers, the period's published shift keys and the `SchedulePublishChange`s a republish would make. The labor rules validator checks each worker's drafts. A `Block` violation stops the publish; a `RequireOverride` one stops it unless `PublishAsync(periodId, actor, overrideReason)` is given a reason, and then `ProceedingAsync` records the overrides. `PlanningPublishResult` carries `Violations` and `NeedsOverride`. `CheckPublishAsync(periodId)` runs the same check without publishing; the planner board shows it.
- **Claims, trades and reassignments.** `LaborRuleShiftAssignmentValidator` is an `IShiftAssignmentValidator`; `Block` and `RequireOverride` violations are problems, warnings aren't.
- **Limits.** `IWorkerScheduleLimitsProvider` gives a worker's rest, days in a row and weekly and daily hours. The built-in one reads `ShiftPlanningOptions` and the worker; with Labor Rules on, a worker's rule set's `MinRest`, `MaxConsecutiveDays`, `MaxHoursPerWeek` and `MaxHoursPerDay` replace them (`FromRuleSet`), the auto-scheduler uses them per worker (`ShiftScheduleWorker.MinRestMinutes` and `MaxConsecutiveDays`), and the built-in validator leaves rest and hours to the rules.

**Predictability pay.** Every `ScheduleChange` is written through a recorder that calls the `IScheduleChangeHandler`s. The labor rules handler skips changes initiated by the worker; for the employer's, it resolves the worker's rule set and, when it has the `PredictabilityPay` rule and the change came less than `NoticeDays` before the shift (the earlier of its times before and after), records `AddedHours` for an added shift, `MovedHours` for a moved one and `RemovedPercent` of a removed one's hours. A reassignment is an added shift for the worker who gets it and a removed one for the worker who loses it. One entry is written per change and worker.

### Example rule sets

:::warning[Examples only]
These show the shape of a rule set. They are not legal advice: check every rule and number with someone who knows the law where your teams work.
:::

```json
{
  "steps": [
    {
      "name": "WorkforceLaborRules",
      "RuleSets": [
        {
          "name": "Example: hourly retail staff",
          "jurisdiction": "Example city with a fair workweek law",
          "teamNames": [ "Boston" ],
          "employmentTypes": [ "FullTime", "PartTime" ],
          "rules": [
            { "ruleKey": "MaxHoursPerDay", "enforcement": "Warn", "parameters": { "Hours": "10" } },
            { "ruleKey": "MaxHoursPerWeek", "enforcement": "RequireOverride", "parameters": { "Hours": "40" } },
            { "ruleKey": "MinRest", "enforcement": "RequireOverride", "parameters": { "Hours": "11" } },
            { "ruleKey": "Breaks", "enforcement": "Block", "parameters": { "ShiftHours": "6", "BreakMinutes": "30" } },
            { "ruleKey": "ScheduleNotice", "enforcement": "Warn", "parameters": { "Days": "14" } },
            { "ruleKey": "PredictabilityPay", "parameters": { "NoticeDays": "14", "AddedHours": "1", "MovedHours": "1", "RemovedPercent": "50" } }
          ]
        },
        {
          "name": "Example: young workers",
          "teamNames": [ "Boston" ],
          "rules": [
            { "ruleKey": "MinorLimits", "enforcement": "Block", "parameters": { "UnderAge": "18", "MaxHoursPerDay": "8", "MaxHoursPerWeek": "40", "LatestEnd": "22:00" } },
            { "ruleKey": "DaysOffPerWeek", "enforcement": "Block", "parameters": { "Days": "1" } }
          ]
        }
      ]
    }
  ]
}
```

The `WorkforceLaborRules` step matches sets by id or name, and teams by `teamNames`.

## Time clock

The **Workforce Time Clock** feature (`CrestApps.OrchardCore.Workforce.TimeClock`) registers its services with `AddWorkforceTimeClockCore()`. A team's `TimeClockPolicyId` names its policy; sub-teams use the nearest parent's (`ITimeClockService.GetPolicyAsync`).

**Punches.** `TimePunch` records are never changed. `ITimeClockService.PunchAsync` records one under the worker's lock, once per `IdempotencyKey` (an offline punch's id, or an event id from another system), when it follows the worker's last punch (an open punch older than 16 hours no longer counts), its source is allowed, it isn't earlier than `BlockEarlyPunchInMinutes` before a shift, and the worker's timesheet for the day is still open. With `RequireGeofence`, browser and app punches must carry a location; one farther than the radius from the team's `Location` is recorded with `WithinGeofence` false. `CorrectAsync` and `RemoveAsync` record a manager's punch with `ReplacesPunchId` (and `Removes` for a removal) and a reason; `ListAsync` returns the punches that count. A contact center or dispatch integration calls `PunchAsync` with its source and the event's id as the key, so a replayed event is recorded once.

**Timesheets.** `ITimesheetCalculator` is pure: it pairs punches into work, break and meal spans, applies grace against the published shift and rounding, closes or flags a forgotten clock-out, splits at midnight in the worker's zone (with NodaTime, so daylight saving days are paid the hours worked), takes the automatic meal off, classifies regular, overtime and double time by day and by the pay periods' week (counting days of the week before the period), adds shift differential premiums, approved time off (`TimeOffType.PayCode`, or the paid time off code) and approved predictability pay, keeps manual lines, and raises attendance exceptions with stable keys. `ITimesheetService` keeps one `Timesheet` per worker and pay period, works it out while it is open, keeps exceptions in step (new ones raised, open ones no longer found removed, decided ones kept), and takes it through `SubmitAsync` (with an attestation), `ApproveAsync`, `RejectAsync`, `ReopenAsync` and `LockPeriodAsync`, which locks only approved timesheets of a period that has ended in the worker's zone. `TimesheetRefresher` runs hourly (`TimesheetRefreshBackgroundTask`) for workers who punched or had a shift in the last 36 hours. With the time clock on, `IWorkedTimeSource` counts punches, so per-hour accrual follows worked time.

**Attendance.** `IAttendanceService` pages exceptions, excuses and confirms them, sums a worker's points in the policy's window, and handles `PunchCorrection` requests: approving one records the punch. When new exceptions take a worker's points to the policy's threshold, every `IAttendancePointsHandler` runs; the built-in one tells the worker's line manager.

**Payroll.** `IPayrollExportService.RunAsync` exports a period's locked timesheets through an `IPayrollExportProvider`, once per provider unless asked again, under the period's lock, keeps the file in a `PayrollExportBatch`, and marks the predictability pay it paid as exported. The built-in `Csv` provider writes the columns in `PayrollCsvSettings` (rates from the worker's `WorkerPayRates`; values that start like a formula are quoted). Add a provider to write another payroll format.

**Kiosks.** A kiosk's address is `workforce/kiosk/{token}`; only the token's hash is stored. Workers identify with their employee number and a PIN (`SetPinAsync`; PBKDF2 with a salt, five wrong PINs lock it for 15 minutes), and the punch carries a two-minute protected ticket.

## My Work portal

The **Workforce My Work Portal** feature (`CrestApps.OrchardCore.Workforce.Portal`) registers its services with `AddWorkforcePortalCore()`. Its pages are front-end routes under `my-work` (`MyWorkController` and `MyTeamController`) with their own layout, so they look the same in any theme, and are allowed by the `UseWorkforcePortal` permission, which the Authenticated role has by default. A user is a worker when `IWorkerManager.FindByUserIdAsync` finds their user id. The time off page needs `RequestTimeOff` (Authenticated by default) and the Time Off feature.

**Notifications.** Services tell workers about changes through `IWorkforceNotifier` with a `WorkforceNotification` (worker, `WorkforceNotificationKind`, title, body and the portal page it opens). Without the portal the notifier does nothing. With it, the notifier delivers to every `IWorkforceNotificationChannel`: the inbox (`Inbox`, which can't be turned off; `IWorkforceInbox` pages it and marks it read), web push (`Push`) and, with the Email feature, email (`Email`), skipping the channels and kinds the worker turned off in their `WorkerPortalSettings` (kept in the worker's properties). A failing channel is logged and the others still run. Other modules add channels, such as SMS. What sends notifications:

- `ISchedulePublishHandler`: one notice per worker with shifts on a first publish, and one per worker whose shifts changed on a republish;
- `IScheduleChangeHandler`: each change outside a publish or approved time off (`ScheduleChange.Source` is `Publish` or `TimeOff` for those), such as a reassignment, a swap or a filled open shift;
- time off approved (also by an allotment or the waitlist), declined and canceled;
- a trade offered to a colleague, and accepted (waiting for a manager), turned down or declined.

**Web push.** `IWebPushService` makes the site's VAPID key pair (P-256) the first time `GetPublicKeyAsync` is called and keeps it in the Workforce collection, the private key protected with ASP.NET Core data protection. `SubscribeAsync` adds a device (`WebPushSubscription`, found again by the SHA-256 of its endpoint), `UnsubscribeAsync` removes it. The push channel encrypts each message for the device (`aes128gcm`) and signs it with VAPID through `Lib.Net.Http.WebPush` (MIT), using the `WorkforceWebPush` named `HttpClient`; a device the push service answers 404 or 410 for is deleted. The payload is `{ title, body, url }`.

**The PWA.** `my-work/manifest.webmanifest` (standalone, scope `my-work`, 192 and 512 pixel icons and a maskable one) and `my-work/sw.js` are served anonymously. The service worker is served with `Service-Worker-Allowed` so its scope covers `my-work`; it shows push notifications, opens their page when clicked, and shows an offline page when a portal page can't load. It caches nothing.

**Calendar feed.** `IWorkerFeedService.GetTokenAsync(worker, renew)` keeps a 256-bit secret in the worker's portal settings; `my-work/feed/{token}.ics` (anonymous) finds the worker by the secret's SHA-256 (`WorkerFeedIndex`), checks the secret itself, and writes the worker's shifts and approved time off from 28 days back to 84 days ahead with `ICalendarWriter`. Renewing the secret stops the old address working.

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

Each feature has its own settings step, matching records by id or name (pay codes by code) so a second run updates them, and naming the records they refer to: `WorkforceShiftPlanning` (`Templates`, with `teamNames` and each segment's `activityName`), `WorkforceTimeOff` (`Types`; `Policies` with `typeName` and `teamNames`; `Allotments` and `BlackoutPeriods` with `teamName` and `typeNames`), `WorkforceLaborRules` (`RuleSets`) and `WorkforceTimeClock` (`PayPeriods`; `PayCodes`; `Policies` with `payPeriodsName` and `teamNames`, which gives those teams the policy). A name that doesn't exist is reported and its record skipped. The **Workforce settings** deployment step writes the steps of the features that are on.

```json
{
  "name": "WorkforceTimeClock",
  "PayPeriods": [ { "name": "Every two weeks", "frequency": "Biweekly", "anchorDate": "2026-01-04", "weekStart": "Sunday" } ],
  "PayCodes": [ { "code": "NIGHT", "kind": "ShiftDifferential", "windowStart": "22:00:00", "windowEnd": "06:00:00", "premiumPerHour": 2 } ],
  "Policies": [ { "name": "Hourly staff", "payPeriodsName": "Every two weeks", "roundingMinutes": 5, "teamNames": [ "Front Desk" ] } ]
}
```

The `workforce-sample` recipe uses every step; it is a starting point to copy.

## Events, workflows and AI tools

Services raise events through `IWorkforceEvents` after saving, named in `WorkforceEvents` (`WorkforceTimeOffRequestedEvent`, `WorkforceSchedulePublishedEvent`, `WorkforcePunchedInEvent`, `WorkforceAttendanceExceptionRaisedEvent`, `WorkforceTimesheetApprovedEvent` and the rest), with an input of `WorkerId`, `TeamId` and the record's ids. Without Workflows nothing hears them; with it, `WorkflowWorkforceEvents` starts the workflows that begin with the event's activity (`IWorkflowManager.TriggerEventAsync`, correlated by the record's id). Each event activity can be limited to some teams. Replace `IWorkforceEvents` to send them elsewhere.

The AI tools (`workforceWhoIsWorking`, `workforceMySchedule`, `workforceTimeOffBalance`, `workforceRequestTimeOff`, `workforceFindCover`, `workforceExplainViolations`) are registered with `AddCoreAITool` when the AI feature is on, each with its feature. They read the signed-in user from the request and use the same permission and scope checks as the admin; a person who manages no team gets an answer saying so.

## Reports

The `Workforce` report data source (`WorkforceReportDataSource`) holds the `IWorkforceReportDataSet` data sets each feature adds: `WorkforcePunches`, `WorkforceTimesheetLines`, `WorkforceAttendanceExceptions` (Time Clock), `WorkforceTimeOffRequests` (Time Off) and `WorkforcePredictabilityPay` (Labor Rules). `WorkforceReportDataSet<TRecord, TIndex>` reads them through their index, limits the rows to the teams the reader manages (`IWorkforceScopeService`, by the index's team column) and adds the worker's and team's names.

## HTTP API

The **Workforce API** feature maps `api/workforce/v1` with the API authentication scheme. Under `me`: `GET me`, `GET me/schedule?from=&days=`, `GET me/balances`, `GET me/time-off`, `POST me/time-off`, `GET me/clock`, `POST me/punch` and `GET me/timesheet?date=`. For the teams the caller manages: `GET teams`, `GET workers?teamId=`, `GET shifts?teamId=&from=&to=` (up to 62 days), `GET time-off/requests`, `POST time-off/requests/{id}/approve` and `/decline`, `POST punches` (Edit punches; `source` may be `Api`, `ContactCenter`, `Kiosk` or `Manager`), `GET timesheets` and `POST timesheets/{id}/approve`. A punch is recorded once per `Idempotency-Key` header; a refused one answers 400 with the reasons.

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
| `ManageLaborRules` (Manage labor rules) | Labor rule sets and the override list. |
| `OverrideLaborRules` (Override labor rules) | Publishing with a reason despite rules that need an override. |
| `ApprovePredictabilityPay` (Approve predictability pay) | Approving and waiving the predictability pay of the teams in one's scope. |
| `ManageTimeClockPolicies` (Manage time clock policies) | Time clock policies, pay periods, pay codes and kiosks. |
| `ApproveTimesheets` (Approve timesheets) | Approving timesheets, attendance exceptions and punch corrections of the teams the user manages. |
| `EditPunches` (Edit punches) | Adding, changing and removing punches. |
| `ExportPayroll` (Export payroll) | Locking periods and exporting payroll. |
| `ViewPayRates` (View pay rates) | Workers' pay rates. |
| `PunchTime` (Clock in and out) | The time clock and timesheet in My Work. Authenticated users have it by default. |
| `UseWorkforcePortal` (Use the My Work portal) | The My Work portal. Authenticated users have it by default. |
| `RequestTimeOff` (Request time off) | Requesting one's own time off, in the self-service portal. Not implied by Manage the whole workforce. |
