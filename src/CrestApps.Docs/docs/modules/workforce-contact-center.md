---
sidebar_label: Workforce for Contact Center
title: Workforce for Contact Center
description: "Contact center workforce management on the Workforce module: a worker for every agent, punches from agent sign-in, and real-time and daily adherence with rule sets, exceptions, alerts and report data sets."
user_manual:
  - user-manual/workforce/contact-center-adherence
  - user-manual/workforce/contact-center-forecasting
---

The **Workforce for Contact Center** module (`CrestApps.OrchardCore.Workforce.ContactCenter`) joins the [Workforce](./workforce.md) module to the Contact Center. Workforce never references the Contact Center; this module references both. It reads the Contact Center's [interval statistics](../contact-center/report-catalog.md#interval-statistics) and agent state log.

The admin screens are described in the User Manual: [Contact Center Adherence](../user-manual/workforce/contact-center-adherence.md) and [Contact Center Forecasting](../user-manual/workforce/contact-center-forecasting.md).

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Workforce.ContactCenter` | Workforce for Contact Center | `CrestApps.OrchardCore.Workforce`, `CrestApps.OrchardCore.ContactCenter.Agents` |
| `CrestApps.OrchardCore.Workforce.ContactCenter.SignInPunches` | Contact Center Sign-in Punches | Workforce for Contact Center, `CrestApps.OrchardCore.Workforce.TimeClock` |
| `CrestApps.OrchardCore.Workforce.Forecasting` | Workforce Forecasting | Workforce for Contact Center, `CrestApps.OrchardCore.Workforce.ShiftPlanning`, `CrestApps.OrchardCore.ContactCenter.IntervalStatistics` |
| `CrestApps.OrchardCore.Workforce.Intraday` | Workforce Intraday | `CrestApps.OrchardCore.Workforce.Forecasting` |
| `CrestApps.OrchardCore.Workforce.Adherence` | Workforce Adherence | Workforce for Contact Center, `CrestApps.OrchardCore.Workforce.ShiftPlanning`, `CrestApps.OrchardCore.ContactCenter.IntervalStatistics` |

## Agents and workers

An agent and a worker are the same person when they share a user account. `AgentWorkerService` lists the agents with their worker, finds the agent of a worker, and creates a worker for an agent (`EnsureWorkerAsync`, `CreateMissingAsync`). A catalog handler on `AgentProfile` creates the worker when an agent is created; it doesn't run on updates, so an agent's frequent presence saves never look the worker up. Agents created before the feature was turned on get their worker from **Workforce > Contact Center Agents**, which can put the new workers in a team.

## Sign-in punches

`SignInPunchHandler` is a Contact Center event handler (`IContactCenterEventHandler`, delivered at least once through the Contact Center outbox). On an `AgentStateChanged` event:

- **Sign-in** (source `SignIn`, or Offline to any other state): the agent's worker is clocked in with source `ContactCenter`, at the time of the change, unless they are already clocked in another way.
- **Sign-out** (source `SignOut`, or any state to Offline): the worker is clocked out, but only when the clock-in that opened their stretch came from the contact center.
- Each punch carries `contact-center:<event id>` as its idempotency key, so an event delivered twice punches once. A punch the time clock refuses (no policy, the contact center not allowed by the policy) is logged and skipped.

## Adherence

`AdherenceCalculator` is pure. It builds the schedule from published shifts (the time around a shift's segments is work) and compares it with the agent's states:

- A moment of scheduled time is **in adherence** when the agent's state is one the scheduled activity's rule allows, with its reasons when the rule names some.
- **Grace:** between two activities of the same shift, the agent may still be in the previous activity for the new one's start grace, and already in the next for the old one's end grace. The start and end of a shift have no grace.
- A slip shorter than the rule's **shortest slip** is forgiven.
- **Exceptions** leave scheduled time out of the share.
- **Adherence** is the time in adherence over the scheduled time less exceptions. **Conformance** is the time in a working state (available, offered work, busy, wrap-up), scheduled or not, over the scheduled work.

`AdherenceRuleSet` holds one rule per kind of activity (or per activity code) for a set of teams. A team uses its own set or its nearest parent's; otherwise the defaults (`AdherenceCalculator.DefaultRules`): work is available, offered work, busy or wrap-up; breaks and meals are a break; meetings, training and coaching are their own states (or away, do not disturb); time off is signed out; travel and on call are anything. Grace is two minutes, the shortest slip one minute, and alerts come after five minutes.

`AdherenceService`:

- `CurrentAsync` and `BoardAsync` give each agent's state now against what is scheduled now, from the agent profile's presence and one calendar query for every shift in the 14 hours around now.
- `RaiseAlertsAsync` raises the **Agent Out Of Adherence** workflow event (`WorkforceEvents.OutOfAdherence`) for an agent out longer than the rule's alert time, once per episode (the worker, the scheduled activity's start and when they went out), recorded in `AdherenceAlert`. The **Workforce Adherence Alerts** background task runs it every minute.
- `ComputeDayAsync` and `SaveDayAsync` work out a worker's day in their time zone (`IWorkerTimeZoneService`: theirs, else their team's, else the site's) from the schedule, the agent's state spans (`IAgentStateHistory`, the same timeline the Contact Center workforce reports build) and the exceptions, and save it as an `AdherenceDay` with its segments. The **Workforce Adherence History** background task saves every agent's previous day each hour once it is over.
- Exceptions are `AdherenceException` documents by worker, team and time.

The workflow event's input has `WorkerId`, `WorkerName`, `TeamId`, `Status`, `Reason`, `ScheduledActivity`, `OutSinceUtc` and `Minutes`.

### Reports

The **Workforce** report data source gains two data sets, limited to the reader's teams and to readers with **View adherence**:

- **Adherence days** (`WorkforceAdherenceDays`): one row per agent and day with adherence and conformance percentages and the hours they come from.
- **Adherence segments** (`WorkforceAdherenceSegments`): one row per stretch of a scheduled day, with the scheduled activity, the agent's state and reason, and whether it was in adherence or covered by an exception.

## Forecasting

A `PlanningGroup` is a set of queues (optionally one channel) forecast and staffed together, with its service goal (a service level such as 80% within 20 seconds, an average speed of answer, or both), the highest occupancy, the contacts an agent handles at once, the customers' patience, the shrinkage, the weeks of history and the team that staffs it. A `ForecastEvent` scales the volume of its days (a closure multiplies by zero), for some groups or all, once or every year.

`ForecastService.CreateAsync` reads the group's [queue intervals](../contact-center/report-catalog.md#interval-statistics) in the team's time zone and builds a draft `Forecast`:

- **Daily volume** from the weighted history (the same weekday of the last weeks, the latest weighing most) or Holt-Winters (additive triple exponential smoothing with a weekly season, from two weeks of history). **Auto** back-tests both on the last two weeks and keeps the lower weighted error; each forecast records both errors (MAPE and WAPE).
- **Events** scale their days.
- **Intervals:** each day is split into 15-minute intervals by its weekday's share of volume over the history, so closed hours stay empty. The **handle time** of an interval is the trimmed mean of the same weekday and time, or the overall average.
- **Staffing** (`ErlangCalculator`, pure): Erlang C from the stable Erlang B recursion, solving for the fewest agents that meet the service level, the average speed of answer and the occupancy cap; chats and messages divide the handle time by the contacts at once; with a patience, Erlang A from the birth-death chain with abandonment, its answer times approximated by Erlang C on the answered load. The agents to schedule are the agents needed over one minus the shrinkage.

`AdjustAsync` changes the volume of some days by a percentage and staffs them again. `ActivateAsync` makes a forecast its group's active one (the one before is superseded) and writes the staffing as the team's coverage requirement named *Forecast: group name* (`CoverageSource.Forecast`), one row per run of intervals that need the same people on each date: the minimum is the agents needed and the target the agents to schedule. The planner board and the auto-scheduler use it unchanged. `ActiveIntervalsAsync` gives the active forecast's intervals of a stretch of time.

## Intraday

`IntradayService.DayAsync` gives a planning group's day, every 15 minutes, in the team's time zone: the active forecast's volume, handle time and agents needed; the actual volume, handle time and service level from the interval statistics (today's are computed every 15 minutes); the agents on published shifts working then (`ICoverageService`); and the agents available or working, from the agent intervals of the team's workers.

- **Reforecast:** `IntradayCalculator.ReforecastFactor` compares the work so far with its forecast and follows half the difference (between 0.25 and 4); `ReforecastAsync` scales the rest of the day's active forecast by it (`ForecastService.ScaleAsync`), staffs it again and updates the team's coverage.
- **Quietest times:** `IntradayCalculator.FindWindows` finds where taking some people off the phones for a while (a meeting, training, coaching) adds the least shortfall, among the times with that many people scheduled.
- **Offers:** an `IntradayOffer` gives the staffing team voluntary overtime or time off over a stretch of the day, for a number of places, first come, first served. `TakeAsync` lengthens the worker's shift that ends as the overtime starts (or starts as it ends), or cuts time off from the start or end of a shift, through `IShiftService.ChangeTimesAsync` with the worker as the initiator, so the change is logged and checked against their rules and earns no predictability pay. Workers take offers on the **Offers** page of My Work, which the feature adds through the new `IMyWorkLinkProvider` seam of the portal's menu.

`IShiftService.ChangeTimesAsync` (in Workforce) moves the start or end of a published shift: it checks the worker's rules with the new times, updates the calendar event and the schedule's draft, drops segments outside the new times, and records a *Moved* schedule change with who asked for it.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageWorkforceAdherence` (Manage adherence rules and exceptions) | Adherence rule sets, recording and removing exceptions, and saving a worker's day again. Implied by Manage the whole workforce. |
| `ManageWorkforceForecasts` (Manage forecasts) | Planning groups, forecast events and forecasts, making a forecast the team's coverage, and the intraday page and its offers. Implied by Manage the whole workforce. |
| `ViewWorkforceAdherence` (View adherence) | The real-time board, workers' days and the exception list, for the teams one manages, and the adherence data sets. |

The Contact Center Agents page needs **Manage workers**.
