---
sidebar_label: Dispatch
title: Dispatch
description: "Visits for work orders, technicians found by skills, certifications, territory, shifts, time off and travel, assignment under a lock per technician with a job on their calendar, and a live dispatch board."
user_manual:
  - user-manual/field-service/dispatch
---

The **Dispatch** module (`CrestApps.OrchardCore.Dispatch`, with `Dispatch.Abstractions` and `Dispatch.Core`) plans who does each job and when. A work order gets a visit; the visit is given a technician and a start, moved along as the technician works, and shown on a board with everyone's day.

The admin screens are described in the User Manual: [Dispatch](../user-manual/field-service/dispatch.md).

## Features

| Feature | Id | Depends on |
| --- | --- | --- |
| Dispatch | `CrestApps.OrchardCore.Dispatch` | Work Orders, Workforce, Geo |

It works with other features when they are on:

| Feature on | What it adds |
| --- | --- |
| SignalR (`OrchardCore.SignalR`) | Open boards reload when any visit changes (`DispatchHub`); without it, they reload each minute. |
| Workflows | The **Visit Assigned** and **Visit Status Changed** events. |
| Skills | Work order types can need skills (with a lowest level) and certifications; the candidate finder checks them on the day. |

## Records

Visits live in the `Dispatch` YesSql collection.

- **`ServiceVisit`**: `Number` (`V-000001` and on), `WorkOrderId`, copies of the work order's number, summary, type, priority, territory and resolution target for lists, `Status` (`Unscheduled`, `Scheduled`, `Dispatched`, `EnRoute`, `OnSite`, `Paused`, `Completed`, `Incomplete`, `Canceled`), `DurationMinutes`, `StartUtc` and `EndUtc`, the arrival window (`ArrivalStartUtc`, `ArrivalEndUtc`), `LeadWorkerId` and `WorkerIds`, `Pinned`, the trip from the stop before (`TravelMinutes`, `TravelKm`), `IncompleteReason` and `ReturnVisitId`, `CalendarEvents` (the job event kept for each worker) and `History`.
- **`DispatchSettings`** (site settings): whether a new work order gets a visit, the arrival window, the look-ahead, the buffer between visits, whether the territory is strict, the board's time zone and the `Objective` weights (travel, urgency, proficiency, preferred technician, workload balance).

`ServiceVisitIndex` keeps the number, work order, status, whether it is open, priority, territory, lead, times and resolution target; `VisitWorkerIndex` has a row per worker of a scheduled visit with its times and whether it keeps them busy.

## Behavior

`IDispatchService` (`DefaultDispatchService`) does every change to a visit, and the pages go through it.

- **Visits.** A new work order gets an unscheduled visit (`IWorkOrderObserver`), unless the setting is off. `CreateMissingAsync` gives one to every open work order without, such as those made before the feature was on. A changed work order updates its open visits' copies (`IWorkOrderHandler`), and a canceled one cancels its open visits.
- **Assigning.** `AssignAsync` gives a visit its workers (the first leads) and start, or moves it. It takes a distributed lock per worker (`DISPATCH_RESOURCE_Worker_{id}`) and on the visit, in a fixed order, before reading anything; then checks, for each worker, that the time is on a published shift, off breaks and time off, clear of their other visits with the travel from the visit before and to the one after, plus the buffer. It saves before letting the locks go, so the next dispatcher's check sees the change: of two dispatchers putting one technician on overlapping visits, one wins and the other is told who is in the way. The visit gets its arrival window (centred on the start), the lead's trip, and a **Job** event on each worker's team Workforce calendar (busy, attended by the worker) through `VisitCalendarWriter`; the work order moves to Scheduled.
- **Field statuses.** `ChangeStatusAsync` moves a visit only along the usual statuses (`VisitStatusRules`). En route and on site start the work order; canceling needs a reason and removes the job events; an incomplete visit needs a reason and makes a return visit for what is left. Unassigning puts a visit back with the unscheduled ones, and a pinned visit stays where it is until unpinned.
- **Candidates.** `IDispatchCandidateFinder` (`DefaultDispatchCandidateFinder`) looks at every active worker between two times and the work order's window. It rules out a worker the work order leaves out, one without a needed skill at its level or a needed certification valid on the day, one outside the territory when it is strict, one with no shift, on time off, or not free long enough with the trip; each is listed with the reason. The others get their earliest start (`SlotFitter`) and a score: the sum of the objective's weighted parts, each between 0 and its weight, shown with the candidate. Warnings say when a candidate works outside the territory, would end after the work order is due, or when travel can't be counted.
- **Travel** comes from `IGeoService` between the job's place and where the worker comes from: the visit before, or their home base, or their team's place.

Times on the pages and in messages are in the worker's or team's time zone, a parent team's, or the dispatch or site time zone.

## The board

**Field Service > Dispatch Board** shows a day: a row per technician (filtered by team) with their shift bands, time off and visits colored by status, the trip before each, and the time now; the unscheduled visits, most urgent first; and every visit on a Leaflet map. A visit is dragged onto a row at its start (to the quarter hour), which assigns it through `AssignAsync`, or **Find technician** lists the best candidates to assign with a click. The board reads `dispatch/board/data` and moves with `dispatch/board/move`.

## Territories

Geo's territory page lists who serves a territory through `ITerritoryMemberSource`; Dispatch adds **Technicians** (kind `Worker`) and **Teams** (kind `Team`, whose members serve it).

## Workflow events

| Event | Starts when | Input |
| --- | --- | --- |
| Visit Assigned | A visit is scheduled, reassigned or moved. | `PreviousStatus`, `Moved` |
| Visit Status Changed | A visit is dispatched, en route, on site, paused, completed, incomplete or canceled. Can be limited to some statuses. | `PreviousStatus` |

Each has `VisitId`, `Number`, `WorkOrderId`, `WorkOrderNumber`, `Status`, `LeadWorkerId`, `WorkerIds`, `StartUtc`, `EndUtc`, `ArrivalStartUtc` and `ArrivalEndUtc`.

## Extending

- `IVisitObserver`: hear about visit events after they happen.
- `IDispatchSettingsProvider`: give the settings from somewhere else.
- `WorkOrderPanels` (Work Orders): a display driver for it adds a panel to the work order's page; Dispatch adds the visits.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageDispatchSettings` (Manage dispatch settings) | The dispatch settings. |
| `ManageDispatch` (Dispatch visits) | Assigning, moving and moving along visits. Implied by Manage dispatch settings. |
| `ViewDispatch` (View visits and the dispatch board) | The visits and the board. Implied by Dispatch visits. |
