---
sidebar_label: Time Off
title: Time Off
description: "Set up time-off types and policies that accrue, see and adjust workers' balances, request time off for a worker, approve or decline requests, let allotments approve on their own with a waitlist, and block busy dates."
technical_manual:
  - modules/workforce
---

Time Off keeps track of the time workers may take and the time they ask for. You set up **types** of time off, such as vacation or sick time, and **policies** that say how workers earn each type and who they apply to. Each worker has a **balance** under each policy, kept as a ledger of every change. A **request** is checked against the balance, the worker's other time off and **blackout dates**; an **allotment** says how many people of a team may be off on a day, approves the requests that fit, and puts the rest on a waitlist. Approved time off goes on the team's Workforce calendar.

| | |
| --- | --- |
| **Menu** | Workforce > Time Off, Workforce > Time-Off Types, Workforce > Time-Off Policies, Workforce > Time-Off Allotments, Workforce > Blackout Dates |
| **Permission** | Approve time off, Adjust time-off balances, Manage time-off policies, Manage the whole workforce |
| **Feature** | Workforce Time Off (it needs Workforce) |

<AskYourAdmin />

A manager sees and decides the time off of the teams they manage, as described in [Workers and Teams](./workers-and-teams.md#who-can-see-what). Types, policies, allotments and blackout dates are set up by someone with **Manage time-off policies**.

## Time-off types

Open **Workforce > Time-Off Types** and choose **Add time-off type**.

| Field | What it does |
| --- | --- |
| **Name** | The type's name, such as *Vacation*, *Sick* or *Jury duty*. |
| **Description** | Shown when a type is chosen. |
| **Counted in** | Hours or days. |
| **Hours in a day** | What a weekday counts when the worker has no shift that day. |
| **Color** | How the time off is drawn on the calendar. |
| **Pay code** | The code payroll pays it under, if any. |
| **Paid** | Whether the time off is paid. |
| **Needs a balance** | A request is refused when the worker's policy for this type doesn't have enough. |
| **Part of a day can be requested** | Allows some hours of one day. |
| **Needs a reason** | A request must say why. |
| **Takes a place in allotments** | Turn off for time off that is never limited, such as jury duty. |
| **Workers can request it** | Turn off for a type only managers record. |

### What a day counts

A day on which the worker has published shifts counts the paid time of those shifts, so an 8-hour shift with an unpaid half-hour meal counts 7.5 hours. A weekday with no shift counts the type's **Hours in a day**. Saturday and Sunday with no shift count nothing. For a type counted in days, each day counts one day, and part of a day counts its share of **Hours in a day**.

## Time-off policies

A policy says how workers earn one type of time off and keep it. Open **Workforce > Time-Off Policies** and choose **Add time-off policy**.

| Field | What it does |
| --- | --- |
| **Time-off type** | The type the policy is for. |
| **Workers earn** | Nothing (balances change only by hand), a set amount each period, an amount for each hour worked, or the year's amount at the reset date. |
| **Amount** | Per period, per hour worked or per year, in the type's unit. |
| **Period** | Weekly, every two weeks, twice a month (the 1st to the 15th and the 16th to the end) or monthly. A weekly or two-week period starts on the date you give, or on a Monday. |
| **Rates by service** | Higher amounts after months of service, one per line, such as `12 = 10`. Service counts from the worker's seniority date, or else their hire date. |
| **Waiting period** | How many days after the hire date accrual begins. |
| **Most the balance can reach** | Accrual stops while the balance is at this amount. |
| **Reset date** | The start of the policy's year. The yearly amount is given then, and what is above the carryover limit expires. |
| **Most that carries over** | What is kept at the reset date. Leave it empty to keep everything. |
| **Can go below zero by** | How much a worker may borrow against what they will earn. |
| **Teams** | The teams the policy applies to, with the teams under them, from and to the dates you give. |

Each period is earned when it ends. Hours worked are the paid time of the worker's published shifts in the period. A worker who becomes eligible during the year gets the share of the yearly amount left in that year.

A worker on several teams gets, for each type, the policy of the nearest team: their own team before the teams above it. A policy assigned to the worker alone, on their **Time off** page, comes first.

Accrual runs every hour and adds what each policy owes for the periods that have ended. Each period is added once, however often accrual runs.

## Balances

In **Workforce > Workers**, open a worker's **Actions** menu and choose **Time off**. The page shows, for each policy that applies to them today:

| Column | What it means |
| --- | --- |
| **Balance** | The sum of the ledger up to today. |
| **Approved, to come** | Approved time off that hasn't started yet. |
| **Waiting** | Requests that wait for a place or a decision. |
| **Available** | What can still be requested: the balance less the two columns before it. |

The **Ledger** lists every change, newest first: opening balances, what was earned, taken, given back, carried over, expired or paid out, and corrections, with who made each change.

With **Adjust time-off balances**, **Change a balance** adds an opening balance (such as one brought from another system), a correction, a carryover or a payout. A negative correction takes away; a payout always takes away. With **Manage time-off policies**, **Policies of their own** gives the worker a policy from and to a date, which replaces their teams' policy of that type.

## Requesting time off

Open **Workforce > Time Off** and choose **Request time off** to record a request for a worker of a team you manage.

| Field | What it does |
| --- | --- |
| **Worker** | Who the time off is for. |
| **Type** | The type of time off. |
| **Days** | The first and the last day. |
| **Only part of the first day** | Asks for some hours of one day. |
| **Reason** | Seen by the managers who decide. Some types need one. |

Choose **Check first** to see what each day counts, the balance on the first day and after the request, how many published shifts it overlaps, the days on which the allotment is full, and what submitting would do. Choose **Submit** to make the request. It is refused when:

- a day falls in [blackout dates](#blackout-dates) of the worker's team;
- the type needs a balance and the worker doesn't have enough on the first day (see [Balances](#balances));
- the worker already has time off or a request on one of the days;
- the type needs a reason and none is given;
- the days count nothing, because they fall on a weekend and the worker has no shifts then.

A request that is accepted is approved at once when it fits an allotment that approves on its own, put on the waitlist when the allotment is full, and otherwise waits for a manager.

## Deciding on requests

**Workforce > Time Off** lists the requests that wait for a place or a decision, from the teams you manage, earliest first. Filter by team or status to see approved, declined, canceled or withdrawn requests. Tick requests and use the bulk actions to **Approve** or **Decline** several at once.

Choose **View** to open a request. It shows each day and what it counts, the worker's balance on the first day, the worker's published shifts on those days, and who else on the team is off or has asked to be off then. Add an optional note to the worker and choose:

| Button | What it does |
| --- | --- |
| **Approve** | Puts the time off on the team's Workforce calendar and takes it from the balance. A manager can approve a request even when the allotment is full. |
| **Decline** | Turns the request down. |
| **Withdraw** | Takes back a request that hasn't been decided. |
| **Cancel the time off** | For approved time off: gives the balance back, takes it off the calendar, and gives the place to the first request waiting for it. |

### Shifts under approved time off

When approved time off overlaps a worker's published shifts, the team's **Shifts under approved time off** setting decides what happens (see [Team settings](./shift-planning.md#team-settings)):

- **Keep them, flagged for cover** (the default): the shifts stay, and the request and the queue show how many need cover.
- **Take them off the schedule**: the shifts are removed from the calendar and the schedule, so the next publish doesn't put them back, and the change log shows **Removed** as the worker's request.

Shifts that were removed aren't put back when the time off is canceled.

### On the calendar

Approved time off is an event on the team's Workforce calendar that can't be edited there. Requests that wait for a place or a decision show on the same calendar as *Requested* or *Waitlisted* events, which don't make the worker busy.

## Allotments

An allotment says how many people of a team, and the teams under it, may be off on a day. Open **Workforce > Time-Off Allotments** and choose **Add allotment**.

| Field | What it does |
| --- | --- |
| **Team** | The team the allotment covers, with the teams under it. |
| **People who may be off** | A number for each day of the week. |
| **Other numbers on dates** | One per line, a date and a number, such as `2026-12-24 = 1`, for a holiday or a busy day. |
| **Time-off types** | The types it counts. Leave it empty for every type that takes a place in allotments. |
| **Approve requests that fit with no manager** | A request on days with a place left is approved as soon as it is made. |
| **When it's full** | Put the request on the waitlist, or send it to a manager. |

Approved and pending requests each take a place on every day they count something; waitlisted ones don't. When two requests arrive at once for the last place, one gets it and the other is waitlisted.

When time off is canceled or a pending request is withdrawn, the waitlisted requests for those days are looked at in the order they were made. Each one that now fits every allotment it falls under is approved, or sent to a manager when an allotment doesn't approve on its own.

## Blackout dates

Open **Workforce > Blackout Dates** and choose **Add blackout dates** to block time off on dates such as a busy season.

| Field | What it does |
| --- | --- |
| **Days** | The first and the last day. |
| **Team** | The team, with the teams under it. Leave **Every team** to block them for everyone. |
| **Time-off types** | The types it blocks. Leave it empty to block every type. |
| **Message** | What is shown when a request falls on these dates. |

## Permissions

| Permission | Allows |
| --- | --- |
| **Approve time off** | The time-off queue, requesting time off for a worker, deciding and canceling, and a worker's balances, for the teams you manage. |
| **Adjust time-off balances** | Changing the balances of the workers of the teams you manage. |
| **Manage time-off policies** | Types, policies, allotments and blackout dates, and giving a worker a policy of their own. |
| **Request time off** | Requesting one's own time off, in the [My Work portal](./my-work-portal.md). |
