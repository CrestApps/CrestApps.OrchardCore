---
sidebar_label: Dispatch
title: Dispatch
description: "Schedule each work order's visit with a technician who has the skills and is free, see everyone's day on the board, and move visits along as the work is done."
technical_manual:
  - modules/dispatch
---

A visit is one trip to do a work order's job. Dispatch finds who can do it, books them without double-booking, and puts the job on their calendar.

| | |
| --- | --- |
| **Menu** | Field Service > Dispatch Board, Visits; Settings > Dispatch |
| **Permission** | Dispatch visits, or View visits and the dispatch board to see them; Manage dispatch settings for the settings |
| **Feature** | Dispatch; SignalR for a board that updates by itself |

<AskYourAdmin />

## Before you start

- Technicians are workers in a team, with published shifts. Give them a home base, or their team a place, so travel is counted from there.
- On a work order type, under **Who can do it**, choose the skills the job needs (with the lowest level) and the certifications the technician must hold.
- On a territory's page (**Territories**), choose under **Who serves it** the technicians and teams that work there.
- On a work order, place the job on the map: click the map under the address, or drag the pin. Without a pin, the address is placed in the background when a geocoding provider is set.

## Schedule a visit

Every new work order gets an unscheduled visit. Work orders made before Dispatch was on get theirs from **Field Service > Visits**: select **Make their visits**.

1. Open the visit from the work order's **Visits** panel, the visits list or the board.
2. Under **Schedule it**, choose the day and select **Find technicians**. The best technicians are listed with their earliest start, the trip there and a score; hover the score to see what it is made of. **Ruled out** says why the others can't.
3. Select **Assign** next to one. Or choose who and when yourself under **Or choose who**; the first one chosen leads.

The visit gets an arrival window to tell the customer, the job shows on the technician's calendar, and the work order becomes *Scheduled*. If the technician isn't free then, the page says who or what is in the way.

## The board

**Field Service > Dispatch Board** shows a day:

- a row for each technician, with their shift in green, time off hatched, their visits colored by status, and a dashed bar for the trip before each;
- the unscheduled visits on the right, most urgent first;
- every visit on the map.

Drag a visit onto a technician's row at the time it should start to schedule or move it. Select **Find technician** on an unscheduled one to see the best choices and assign with a click. Choose another day with the arrows or the date, and a team to see only its technicians. With SignalR on, the board shows **Live** and changes as others work.

## Move a visit along

On the visit's page, under **Move it along**, choose what happened: dispatched, en route, on site, paused, completed, or incomplete. En route and on site start the work order. An incomplete visit needs a note saying why, and gets a return visit for what is left. Canceling needs a reason too.

**Unschedule** takes a visit off its technicians; **Pin** keeps it where it is, so it isn't moved by mistake.

## Settings

**Settings > Dispatch** sets how long the arrival window is, the minutes kept free between visits, how far ahead to look, the time zone of the board, whether only a territory's technicians are offered, and how much each thing counts when technicians are ranked: a short trip, the soonest start, skill level, the preferred technician and an even workload.
