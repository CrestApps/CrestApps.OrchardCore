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
| Field Service API | `CrestApps.OrchardCore.Dispatch.Api` | Dispatch |

It works with other features when they are on:

| Feature on | What it adds |
| --- | --- |
| SignalR (`OrchardCore.SignalR`) | Open boards reload when any visit changes (`DispatchHub`); without it, they reload each minute. |
| Workflows | The **Visit Assigned** and **Visit Status Changed** events. |
| Skills | Work order types can need skills (with a lowest level) and certifications; the candidate finder checks them on the day. |
| Fleet (`CrestApps.OrchardCore.Assets.Fleet`) | Work order types can need equipment (asset types); assigning books a free one on its Equipment calendar. |
| SMS (`OrchardCore.Sms`), Email (`OrchardCore.Email`) | Customer notices, by text when the contact has a phone, else by email. |
| CrestApps AI (`CrestApps.OrchardCore.AI`) | The field service [AI tools](#ai-tools). |
| Data Pipelines (`OrchardCore.DataPipelines.Core`) | The **Field Service** report data source, with work orders and visits, and with Assets on, assets, meter readings and service history ([Reports and KPIs](#reports-and-kpis)). |
| Scheduling (`CrestApps.OrchardCore.Scheduling`) | Appointment types that make work orders offer only the times a technician can take the job ([Booking capacity](#booking-capacity)). |
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

- **The pack.** The page keeps the technician's visits from today through `OfflineDays` on the device (IndexedDB), with each work order's address, contact, access notes, checklists and answers, lines, notes, signatures and photo count, the statuses a technician can move to, and the stock locations with the units each has free of the parts on the pack's lines (`IFieldStockSource.AvailableAsync`, from Inventory). With Assets on, each work order also has its equipment (`IFieldAssetSource`: number, name, make and model, serial number) and its history: the latest five other work orders on the same assets. Visits under way from the day before come too.
- **The outbox.** Every change is applied to the pack at once and kept as a command with a key the device makes: a status, an answer, a note, parts used, a signature (a drawn image as a data URL, or a typed name), a photo (made at most 1600 pixels on its long side, as JPEG, on the device) or a follow-up (`FollowUp`, with what it is for: `WorkOrderService.FollowUpAsync` makes a work order of the same type for the same customer, place and assets, and a note on this one names it). Commands are sent in order whenever the device is online: at once, then every 30 seconds, and when it reconnects. After each sync, the server's pack gets the commands still waiting applied on top, so the page never goes back.
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

## Booking capacity

With Scheduling on, `DispatchCapacitySlotFilter` (an `IAppointmentSlotFilter`) narrows the open times of every appointment type a work order type lists under **Bookings**. For each slot it asks the candidate finder about the job a booking would make: the type's skills, certifications, crew size and length, arriving in the slot (`IDispatchCandidateFinder.FindAsync` with a work order that isn't saved). The slot stays while enough technicians are on a published shift and free for the whole job, less one for each booking of the type already made whose job would overlap and that no one has been given yet (status *New* or *Ready*); `Remaining` is lowered to what is left. A booking being moved doesn't count against itself. Appointment types no work order type lists keep their slots.

## Reports and KPIs

**Reports > Field Service > KPIs** (`FieldServiceKpisController`, View visits and the dispatch board) shows, for a range of days (the last 30 by default, at most a year) in the dispatch time zone, from the visits that started and the work orders made in it:

| KPI | Counted as |
| --- | --- |
| First-time fix | Finished visits (completed or incomplete) completed without a return visit. |
| On-time arrival | Visits whose first *On site* is by the end of the arrival window (or the start, without one). |
| Jobs per technician a day | Completed visits per lead technician, on the local days they did any. |
| Return visits | Finished visits that made a return visit. |
| Travel | Travel minutes to finished visits against their minutes on site (first *On site* to *Completed* or *Incomplete*). |
| Answered in time, resolved in time | Work orders answered by their response time, and completed by their resolution time, of those that have one. |
| Time to repair | The mean hours from making a work order to completing it. |

| Revenue per work order | What the completed work orders billed on average (`WorkOrderService.BillableAmount`), in the currency most of them bill in. |
| Repeat work | Completed work orders with assets that had another work order on one of the assets within the days chosen (30 by default), made after they were completed. |

Below the tiles, **Parts used by stock location** lists the parts the completed work orders used from each location, such as a van, with how many work orders used them.

`FieldServiceKpiService` reads at most 5,000 visits and 5,000 work orders a range; the page says so when there are more.

With **Data Pipelines** on, the **Field Service** report data source (`FieldServiceReportDataSource`) has these data sets for the Report Builder:

- **Work orders** (`FieldServiceWorkOrders`, View work orders): number, type, status, priority, summary, territory, customer, source, when made, answered and completed, the response and resolution times and whether they were met, hours to complete, parts used, and the work order followed up.
- **Visits** (`FieldServiceVisits`, View visits and the dispatch board): number, work order, status, lead technician, crew size, territory, start and end, travel minutes and kilometers, when they arrived and whether in the window, minutes on site, whether the job was done the first time, and why not finished.

- With Assets on:
  - **Assets** (`FieldServiceAssets`, View assets): number, name, type, state, owner kind and id, the asset it is part of, serial number, make and model, city and postal code, when installed, when the warranty ends, whether it can be booked and carries stock, and when it was added.
  - **Meter readings** (`FieldServiceMeterReadings`, View assets): the asset, meter, value, when it was read, and its source.
  - **Service history** (`FieldServiceServiceHistory`, View work orders and View assets): a row for each asset on each work order, with the work order's number, type, status, summary, when it was made and completed, the parts used and the work order followed up.

They filter by the day they were made (or read), and by their id, type, worker, owner and status columns, in the index. More data sets are added through `IFieldServiceReportDataSet`.

## AI tools

With CrestApps AI on, these tools can be chosen for an AI profile, under **Field Service**. Every tool but the last runs as the signed-in user, with the same permission as the admin page.

| Tool | Name | Permission | Does |
| --- | --- | --- | --- |
| Create a work order | `workOrderCreate` | Manage work orders | Creates a work order with the problem, priority, contact, service address and access notes, and gives its number; when no type is named and there are several, it lists them. |
| Work order status | `workOrderStatus` | View work orders | A work order's status, priority, response and resolution times, and its visits with their technicians, times and ETA. |
| Summarize a work order | `workOrderSummarize` | View work orders | The details and history (notes, checklist answers, parts used, signatures, photos and visits) for a technician's briefing. |
| Find a technician | `dispatchFindTechnician` | View visits and the dispatch board | Who can do a visit on a day, best first, and who is ruled out and why. |
| Optimize and explain a day | `dispatchOptimizeAndExplain` | Dispatch visits | The proposal for a day (each move, the travel before and after, the visits that fit nobody); called again with `apply` and the approved visit numbers, it applies only those. |
| Reschedule a visit | `dispatchRescheduleVisit` | Dispatch visits | Moves a visit to another time or technician, with the board's checks. |
| Where is my technician | `dispatchWhereIsMyTechnician` | None | For an AI agent or voice line that talks to customers: the caller's visits from today, found by their own phone number (the last ten digits of the work order's contact phone) or their tracking link's token, with only the status, the arrival window, the ETA when on the way and the technician's first name. |

Times are given and read in the dispatch time zone.

## Field Service API

The **Field Service API** feature maps HTTP endpoints for other systems and apps, with the API authentication scheme (`OrchardCoreConstants.AuthenticationSchemes.Api`, such as an OpenID token). Each endpoint checks the same permission as the admin page, and answers JSON with enum names as strings. Errors come back as RFC 9457 problem details with the reason. When OpenAPI is on, the endpoints are listed under the **Work Orders**, **Dispatch**, **Geo** and **Assets** tags.

### Work orders: `api/work-orders/v1`

| Endpoint | Permission | Does |
| --- | --- | --- |
| `GET types` | View work orders | The work order types. |
| `GET checklists` | View work orders | The checklist templates with their questions. |
| `GET work-orders?status=&open=&q=&skip=&take=` | View work orders | Work orders, newest first, with the count; `q` matches the number or summary. |
| `GET work-orders/{id}` | View work orders | A work order with its lines, checklists, notes and signatures. |
| `POST work-orders` | Manage work orders | Creates a work order from `typeId`, `summary`, `description`, `priority`, `customerId`, `accountId` (a CRM account), `contactId`, `contact`, `serviceAddress`, `accessNotes`, `assetIds`, `earliestUtc` and `latestUtc`. With an `Idempotency-Key` header, sending it again returns the work order made the first time. |
| `POST work-orders/{id}/status` | Manage work orders | `{ "status", "subStatusId", "note" }`. |
| `POST work-orders/{id}/notes` | Manage work orders | `{ "text" }`. |
| `POST work-orders/{id}/answers` | Manage work orders | `{ "checklistIndex", "questionId", "value", "answeredUtc" }`. |
| `POST work-orders/{id}/parts` | Manage work orders | `{ "lineId", "quantity", "locationId" }`. |
| `POST work-orders/{id}/signatures` | Manage work orders | `{ "name", "imagePath" }`. |
| `POST work-orders/{id}/attachments` | Manage work orders | `{ "path" }`, a file in the media library. |
| `POST work-orders/{id}/complete` | Manage work orders | Completes it, or answers what is missing. |
| `GET agreements?status=&q=&skip=&take=` | Manage service agreements | Service agreements, highest number first, with the count; `q` matches the number or name. With Service Agreements on. |
| `GET agreements/{id}` | Manage service agreements | An agreement with its schedules. |

### Visits: `api/dispatch/v1`

| Endpoint | Permission | Does |
| --- | --- | --- |
| `GET board?day=&teamId=` | View visits and the dispatch board | A day's board (today in the dispatch time zone by default): the technicians, of a team and the teams under it when given, with their open time and time off, the visits scheduled that day and the ones waiting. |
| `GET visits?from=&to=&workerId=` | View visits and the dispatch board | Visits scheduled between two UTC times (at most 31 days apart), for one technician or all. |
| `GET visits?unscheduled=true` | View visits and the dispatch board | The visits waiting for a time. |
| `GET visits/{id}` | View visits and the dispatch board | A visit with its history and notices. |
| `GET visits/{id}/candidates?from=&to=` | View visits and the dispatch board | The technicians who can do it, best first, and why the others can't. |
| `POST visits/{id}/assign` | Dispatch visits | `{ "workerIds", "startUtc", "durationMinutes", "pinned" }`, under the same locks as the board. |
| `POST visits/{id}/unassign` | Dispatch visits | Takes it off its technicians. |
| `POST visits/{id}/status` | Dispatch visits | `{ "status", "note" }`. |
| `POST optimize` | Dispatch visits | `{ "day": "yyyy-MM-dd", "workerIds", "includeUnscheduled" }`: the proposal, with its moves and the travel before and after. No technicians given means every one. |
| `POST optimize/apply` | Dispatch visits | `{ "moves" }` from a proposal. |
| `GET positions` | View visits and the dispatch board | Technicians' latest positions from the last 30 minutes, with Technician Mobile. |

The technician app's own endpoints are under `api/dispatch/v1/mobile` (see [Technician Mobile](#technician-mobile)).

### Geo: `api/geo/v1`

| Endpoint | Permission | Does |
| --- | --- | --- |
| `GET geocode?address=` | View visits and the dispatch board | The candidates for an address, from the chosen geocoding provider. |
| `GET territories/resolve?latitude=&longitude=&postalCode=` | View visits and the dispatch board | The territory a place or postal code is in, and how it matched. |

### Assets: `api/assets/v1`

When Assets is on:

| Endpoint | Permission | Does |
| --- | --- | --- |
| `GET assets?q=&typeId=&ownerId=&skip=&take=` | View assets | Assets by number, name or serial number, type or owner. |
| `GET assets/{id}` | View assets | An asset. |
| `GET assets/{id}/readings?take=` | View assets | Its latest meter readings. |
| `POST assets/{id}/readings` | Manage assets | `{ "meter", "value", "utc" }`; a cumulative meter can't go back. |

## Extending

- `IVisitObserver`: hear about visit events after they happen.
- `IDispatchOptimizer`: a better solver; the last one registered is used (the [OR-Tools](or-tools) add-on is one).
- `IDispatchEquipmentService`: equipment from somewhere other than Fleet.
- `IVisitNoticeSender`: another channel for customer notices.
- `IDispatchSettingsProvider`: give the settings from somewhere else.
- `IFieldFileStore`: keep the technician app's photos and signatures somewhere other than the media library.
- `IFieldStockSource`: stock locations for parts used from somewhere other than Inventory.
- `WorkOrderPanels` (Work Orders): a display driver for it adds a panel to the work order's page; Dispatch adds the visits.

## Getting started and the sample

**Get started** on the dispatch board (for **Manage dispatch settings**) opens a checklist (`FieldServiceSetupController`, `dispatch/get-started`): maps and geocoding, territories, technicians, their shifts, work order types, checklists and service levels, assets, customer notices, the Field page and the first visits, each with what is there now and a link to it. It isn't in the menu.

The **Field service sample** recipe (`FieldServiceSample`, in the module's `Recipes` folder) turns on Geo, Assets with Fleet, Work Orders, Workforce with Shift Planning and My Work, Dispatch and Technician Mobile, then adds a Field Operations team with a dispatcher and three technicians, a day shift template, two territories (Cambridge inside Boston), boiler and van asset types with a training boiler and a van that carries stock, a boiler safety checklist, a standard service level, a waiting-on-parts sub-status, and boiler service and no-heat work order types. Its records have fixed ids, so running it again changes nothing. Publish a schedule for the team before dispatching.

## Recipes and deployment

The **Dispatch settings** deployment step exports `DispatchSettings` through Orchard Core's Settings step. Visits aren't exported: they belong to the work orders of the site they were made on. Neither are the technician app's command records, consent and positions, which are about one site's people and work.

In a hand-written recipe, give records an `ItemId` of 26 lowercase letters and digits, as Orchard Core makes them (such as `4fs7kjkcahkr0q5vh31ktg943y`); any other value is replaced with a new id, so records pointing at it lose the link and running the recipe again adds them twice.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageDispatchSettings` (Manage dispatch settings) | The dispatch settings. |
| `ManageDispatch` (Dispatch visits) | Assigning, moving and moving along visits. Implied by Manage dispatch settings. |
| `ViewDispatch` (View visits and the dispatch board) | The visits and the board. Implied by Dispatch visits. |
