---
sidebar_label: Dispatch
title: Dispatch
description: "Schedule each work order's visit with a technician who has the skills and is free, see everyone's day on the board, and move visits along as the work is done."
technical_manual:
  - modules/dispatch
  - modules/or-tools
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

## Optimize the day

On the board, select **Optimize the day**. Tick **Fit in unscheduled** to also place waiting visits. A list of moves comes up, with the day's travel now and with the moves; untick any you don't want and select **Apply the chosen moves**. Pinned visits and visits already started stay where they are. When a waiting visit fits nobody's day, **Find technician** on it says why. With **OR-Tools Dispatch** on, the OR-Tools solver also looks for a better day and the better of the two is proposed; the list says which solver proposed it.

## Crews and equipment

On a work order type, **People needed** sets the crew: a visit is assigned only with that many technicians. With Fleet on, the type's equipment is booked on the equipment's calendar when the visit is assigned, so a lift can't be in two places; if none is free, the page says so.

## Customers

Turn on notices in **Settings > Dispatch > Customer notices**. The work order's contact then gets a text (or an email) when the visit is scheduled or moved, the day before, when the technician is on the way (with a link to follow them), and when it is done or canceled. The link shows the arrival window and, once on the way, when the technician is expected; it stops working when the visit is over. The visit's page lists what the customer was told, and links to the tracking page.

## Visits at risk

A visit not started, or running over, by the minutes set in the settings, one whose technician arrives after the window, or whose technician is off that day, is marked at risk on the board with the reason, and on its page.

## Move a visit along

On the visit's page, under **Move it along**, choose what happened: dispatched, en route, on site, paused, completed, or incomplete. En route and on site start the work order. An incomplete visit needs a note saying why, and gets a return visit for what is left. Canceling needs a reason too.

**Unschedule** takes a visit off its technicians; **Pin** keeps it where it is, so it isn't moved by mistake.

## The technician's Field page

With **Technician Mobile** on, technicians have a **Field** page in My Work, made for a phone. It shows their visits today and in the next two days, and keeps working without a connection, such as in a basement: everything they do is kept on the phone and sent once it is back online. The badge at the top says whether everything was sent, or how many changes are waiting.

On a visit, technicians:

- tap **On my way**, **Arrived**, **Pause** and **Finish the visit**, or **Can't finish** with the reason;
- open **Directions** in the phone's maps app, or **Call** or **Text** the contact;
- answer the checklists, record the parts they used and where from (their van first), and add notes;
- take photos, and have the customer sign with a finger.

**Finish the visit** completes the work order too, so it is refused with the reason until the required answers, the signature and the photos are in.

If you give a visit to someone else while its technician is offline, their changes to it aren't kept: when their phone reconnects, they see that the visit was given to someone else, and it leaves their page.

The first time, the page asks whether to share their location while they work. If they agree, their position is sent only while they have a visit under way or still to come that day, and shows as a dot on today's board, with the time. They can stop at any time, which removes the positions kept so far; positions are removed after 30 days anyway.

## Settings

**Settings > Dispatch** has three cards. **Visits** sets whether new work orders get a visit, the arrival window, the minutes kept free between visits, how far ahead to look, the time zone, and how late a visit is before it is at risk. **Choosing technicians** sets whether only a territory's technicians are offered, how much each thing counts when they are ranked (a short trip, the soonest start, skill level, the preferred technician and an even workload), and how long the optimizer looks. **Customer notices** turns the notices and the tracking link on, sets the reminder hour, and holds the five texts.
