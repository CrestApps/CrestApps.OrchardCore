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
| Technician Mobile | `CrestApps.OrchardCore.Dispatch.Mobile` | Dispatch, Workforce My Work Portal, Media |

It works with other features when they are on:

| Feature on | What it adds |
| --- | --- |
| SignalR (`OrchardCore.SignalR`) | Open boards reload when any visit changes (`DispatchHub`); without it, they reload each minute. |
| Workflows | The **Visit Assigned** and **Visit Status Changed** events. |
| Skills | Work order types can need skills (with a lowest level) and certifications; the candidate finder checks them on the day. |
| Fleet (`CrestApps.OrchardCore.Assets.Fleet`) | Work order types can need equipment (asset types); assigning books a free one on its Equipment calendar. |
| SMS (`OrchardCore.Sms`), Email (`OrchardCore.Email`) | Customer notices, by text when the contact has a phone, else by email. |
| Inventory, with Technician Mobile | The technician app lists stock locations for parts used, the van the technician drives first (`IFieldStockSource`). |

## Records

Visits live in the `Dispatch` YesSql collection.

- **`ServiceVisit`**: `Number` (`V-000001` and on), `WorkOrderId`, copies of the work order's number, summary, type, priority, territory and resolution target for lists, `Status` (`Unscheduled`, `Scheduled`, `Dispatched`, `EnRoute`, `OnSite`, `Paused`, `Completed`, `Incomplete`, `Canceled`), `DurationMinutes`, `StartUtc` and `EndUtc`, the arrival window (`ArrivalStartUtc`, `ArrivalEndUtc`), `LeadWorkerId` and `WorkerIds`, `Pinned`, the trip from the stop before (`TravelMinutes`, `TravelKm`), `IncompleteReason` and `ReturnVisitId`, `CalendarEvents` (the job event kept for each worker) and `History`.
- **`DispatchSettings`** (site settings): whether a new work order gets a visit, the arrival window, the look-ahead, the buffer between visits, whether the territory is strict, the board's time zone, the `Objective` weights (travel, urgency, proficiency, preferred technician, workload balance), the minutes late that put a visit at risk, the optimizer's time limit, and the customer notices (on or off, the tracking link, the reminder hour and the five texts).

`ServiceVisitIndex` keeps the number, work order, status, whether it is open, priority, territory, lead, times and resolution target; `VisitWorkerIndex` has a row per worker of a scheduled visit with its times and whether it keeps them busy.

## Behavior

`IDispatchService` (`DefaultDispatchService`) does every change to a visit, and the pages go through it.

- **Visits.** A new work order gets an unscheduled visit (`IWorkOrderObserver`), unless the setting is off. `CreateMissingAsync` gives one to every open work order without, such as those made before the feature was on. A changed work order updates its open visits' copies (`IWorkOrderHandler`), and a canceled one cancels its open visits.
- **Assigning.** `AssignAsync` gives a visit its workers (the first leads) and start, or moves it. It takes a distributed lock per worker (`DISPATCH_RESOURCE_Worker_{id}`) and on the visit, in a fixed order, before reading anything; then checks, for each worker, that the time is on a published shift, off breaks and time off, clear of their other visits with the travel from the visit before and to the one after, plus the buffer. It saves before letting the locks go, so the next dispatcher's check sees the change: of two dispatchers putting one technician on overlapping visits, one wins and the other is told who is in the way. The visit gets its arrival window (centred on the start), the lead's trip, and a **Job** event on each worker's team Workforce calendar (busy, attended by the worker) through `VisitCalendarWriter`; the work order moves to Scheduled.
- **Field statuses.** `ChangeStatusAsync` moves a visit only along the usual statuses (`VisitStatusRules`). En route and on site start the work order; canceling needs a reason and removes the job events; an incomplete visit needs a reason and makes a return visit for what is left. Unassigning puts a visit back with the unscheduled ones, and a pinned visit stays where it is until unpinned.
- **Candidates.** `IDispatchCandidateFinder` (`DefaultDispatchCandidateFinder`) looks at every active worker between two times and the work order's window. It rules out a worker the work order leaves out, one without a needed skill at its level or a needed certification valid on the day, one outside the territory when it is strict, one with no shift, on time off, or not free long enough with the trip; each is listed with the reason. The others get their earliest start (`SlotFitter`) and a score: the sum of the objective's weighted parts, each between 0 and its weight, shown with the candidate. Warnings say when a candidate works outside the territory, would end after the work order is due, or when travel can't be counted.
- **Travel** comes from `IGeoService` between the job's place and where the worker comes from: the visit before, or their home base, or their team's place.

Times on the pages and in messages are in the worker's or team's time zone, a parent team's, or the dispatch or site time zone.

## Crews and equipment

A visit copies its type's crew size and required equipment. Assigning fewer people than the crew is refused ("This job needs 2 people."). For each required asset type, assigning picks a free one (`IDispatchEquipmentService`; Fleet lists the active schedulable assets of the type) and books it with a **Reservation** on its Equipment calendar, which allows one booking at a time; when none is free the assignment is refused ("No lift is free then."). Moving the visit moves the reservation, and canceling removes it.

## Optimizing a day

`IDispatchOptimizationService.ProposeAsync` builds a day's problem (`DispatchProblem`): the technicians on shift with their open spans and home, the visits that can move (scheduled, single-technician, not pinned or started), the ones that can't (fixed at their time), and, when asked, the unscheduled visits whose window allows the day; the technicians each may do come from `IDispatchCandidateFinder.CheckWorkerAsync` (skills, certifications, territory). Travel times come from one matrix of `IGeoService`. The site's `IDispatchOptimizer` returns a plan; the built-in one (`DefaultDispatchOptimizer`) tries two starts, the day as it is and a fresh one from the fixed visits, fits in the left-out visits by cheapest insertion, and improves each by moving a visit, swapping two and reversing part of a route, within the settings' time limit, and keeps the cheaper. A proposal lists the moves with the travel before and after; nothing changes until `ApplyAsync`, which assigns each move through `AssignAsync` (so every move is locked and checked again), retries moves that wait for another, and unschedules one visit to break a swap.

## Customer notices and tracking

With notices on, `VisitNoticeService` tells the work order's contact when the visit is scheduled or moved, the day before (from the reminder hour), when the technician is on the way, and when it is done or canceled, once for each start (`VisitNotice` on the visit). The texts are set in the settings with `{Company}`, `{Number}`, `{Date}`, `{Window}`, `{Technician}` (first name), `{Eta}` and `{TrackingUrl}`. Senders are `IVisitNoticeSender` (SMS and email are built in).

On the way, the visit gets an ETA: now plus the trip from the technician's last stop, or home. The tracking link (`/track/{token}`, `IVisitTrackingLinks`) is signed with data protection, lasts a week, and stops working once the visit is completed, incomplete or canceled. Its page shows the business, the status, the day, the arrival window, the technician's first name and, on the way, the expected time; it needs no sign-in.

## Visits at risk

Every five minutes `VisitRiskMonitor` flags today's open visits that are running over, not started, arriving after their window, or whose technician is on time off then (the settings say how many minutes count as late). The board marks them and the visit shows the reason; the **Visit At Risk** workflow event starts once for each new reason, and any status change clears the flag until it is checked again.

## The board

**Field Service > Dispatch Board** shows a day: a row per technician (filtered by team) with their shift bands, time off and visits colored by status, the trip before each, and the time now; the unscheduled visits, most urgent first; and every visit on a Leaflet map. A visit is dragged onto a row at its start (to the quarter hour), which assigns it through `AssignAsync`, or **Find technician** lists the best candidates to assign with a click. **Optimize the day** shows a proposal for the day and team shown, with a box for each move, and applies the ones kept. The board reads `dispatch/board/data` and moves with `dispatch/board/move`.

## Territories

Geo's territory page lists who serves a territory through `ITerritoryMemberSource`; Dispatch adds **Technicians** (kind `Worker`) and **Teams** (kind `Team`, whose members serve it).

## Workflow events

| Event | Starts when | Input |
| --- | --- | --- |
| Visit Assigned | A visit is scheduled, reassigned or moved. | `PreviousStatus`, `Moved` |
| Visit Status Changed | A visit is dispatched, en route, on site, paused, completed, incomplete or canceled. Can be limited to some statuses. | `PreviousStatus` |
| Visit At Risk | A visit becomes at risk. | `Reason` |

Each has `VisitId`, `Number`, `WorkOrderId`, `WorkOrderNumber`, `Status`, `LeadWorkerId`, `WorkerIds`, `StartUtc`, `EndUtc`, `ArrivalStartUtc` and `ArrivalEndUtc`.

## Technician Mobile

The **Technician Mobile** feature adds a **Field** page to My Work (`~/my-work/field`) that works offline.

- **The pack.** The page keeps the technician's visits from today through `OfflineDays` on the device (IndexedDB), with each work order's address, contact, access notes, checklists and answers, lines, notes, signatures and photo count, the statuses a technician can move to, and the stock locations. Visits under way from the day before come too.
- **The outbox.** Every change is applied to the pack at once and kept as a command with a key the device makes: a status, an answer, a note, parts used, a signature (a drawn image as a data URL, or a typed name) or a photo (made at most 1600 pixels on its long side, as JPEG, on the device). Commands are sent in order whenever the device is online: at once, then every 30 seconds, and when it reconnects. After each sync, the server's pack gets the commands still waiting applied on top, so the page never goes back.
- **Once and in order.** `IFieldSyncService.ApplyAsync` applies each command once: a key it has seen comes back as `Duplicate` with the first result (from a `FieldCommandReceipt`), so sending the outbox again changes nothing.
- **The server wins.** A command for a visit the technician is no longer on comes back `Rejected` with the code `moved`, and one for a canceled visit with `canceled`; the page shows the reason and the visit leaves the device with the next pack. Answers merge per question: an answer keeps the time it was given on the device, and one given later, on the site or the device, is kept (`stale`). Notes are added, and parts are added movements.
- **Finishing.** Finish completes the work order first, so it is refused with the reason until its required answers, signature and photos are in.
- **Files.** Photos and signatures go to the media library under `work-orders/{number}/`, named by the command's key, so sending one again writes the same file. The work order's page shows them.
- **The page offline.** The My Work service worker keeps the last copy of each page listed in `MyWorkOfflineOptions.Pages` (Technician Mobile adds the Field page) with the scripts, styles, fonts and images it loaded, and shows it when there is no connection.

### Location

A technician shares their position only after agreeing to the notice the page shows (`WorkerLocationConsent`, with the notice's version and a history). The page sends it every `PingIntervalSeconds` while a visit is under way, or one is still to come today; the server keeps it only then, and never off shift. A position on the way to a visit refreshes its ETA. Today's dispatch board shows each technician's position from the last 30 minutes on its map. Withdrawing removes the technician's positions, and an hourly task removes positions after `LocationRetentionDays` and command records after `ReceiptRetentionDays`. Changing `ConsentNoticeVersion` asks everyone again.

### Endpoints

The page calls `my-work/field/api` with its sign-in and an antiforgery header (from `my-work/field/api/token`); other apps call the same endpoints under `api/dispatch/v1/mobile` with the API authentication scheme. The caller must have **Use the My Work portal** and be a worker; requests and answers are JSON with enum names as strings.

| Endpoint | Does |
| --- | --- |
| `GET pack` | The caller's pack. |
| `POST sync` | `{ "commands": [...] }`, oldest first; answers `{ "results": [...] }` with `key`, `visitId`, `outcome` (`Applied`, `Duplicate`, `Rejected`), `code` and `reason` for each. |
| `POST consent` | `{ "given": true }` or `false`. |
| `POST pings` | `{ "pings": [{ "utc", "latitude", "longitude", "accuracyMeters", "speed", "heading" }] }`; answers how many were kept and why the rest weren't. |

### Configuration

The settings are read from the `CrestApps:Dispatch:Mobile` section of the tenant's configuration and described in the module's `ConfigurationSchema.json`.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Dispatch": {
        "Mobile": {
          "OfflineDays": 2,
          "PingIntervalSeconds": 60,
          "LocationRetentionDays": 30,
          "ReceiptRetentionDays": 30,
          "ConsentNoticeVersion": "1",
          "MaxFileKilobytes": 4096,
          "MaxCommandsPerSync": 200
        }
      }
    }
  }
}
```

## Extending

- `IVisitObserver`: hear about visit events after they happen.
- `IDispatchOptimizer`: a better solver; the last one registered is used (the [OR-Tools](or-tools) add-on is one).
- `IDispatchEquipmentService`: equipment from somewhere other than Fleet.
- `IVisitNoticeSender`: another channel for customer notices.
- `IDispatchSettingsProvider`: give the settings from somewhere else.
- `IFieldFileStore`: keep the technician app's photos and signatures somewhere other than the media library.
- `IFieldStockSource`: stock locations for parts used from somewhere other than Inventory.
- `WorkOrderPanels` (Work Orders): a display driver for it adds a panel to the work order's page; Dispatch adds the visits.

## Recipes and deployment

The **Dispatch settings** deployment step exports `DispatchSettings` through Orchard Core's Settings step. Visits aren't exported: they belong to the work orders of the site they were made on. Neither are the technician app's command records, consent and positions, which are about one site's people and work.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageDispatchSettings` (Manage dispatch settings) | The dispatch settings. |
| `ManageDispatch` (Dispatch visits) | Assigning, moving and moving along visits. Implied by Manage dispatch settings. |
| `ViewDispatch` (View visits and the dispatch board) | The visits and the board. Implied by Dispatch visits. |
