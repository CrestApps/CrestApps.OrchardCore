---
sidebar_label: Data sets and reports
title: Data sets and reports
description: "The Tickets data source and its data sets for the Report Builder and data pipelines, the ticket reports, the ticket time reports and Account Health."
user_manual: user-manual/service-desk/reports
---

## Data sets

With Data Pipelines Core on, the **Tickets** data source serves the [Report Builder](../report-builder/data-sources) and data pipelines. Every data set shows only the tickets in the reader's scope.

| Data set | Rows |
| --- | --- |
| `Tickets` | One per ticket as it is now: desk, status and priority with their names, requester, account, team, assignee, channel, times, minutes to first response and to solve, replies, reopens, reassignments, escalation level, links and participants. `TicketId` references the `Ticket` type in `Contents`, for its custom fields |
| `TicketEntries` | One per public reply and internal note |
| `TicketChanges` | One per recorded field change: the field, its value before and after, who and when |
| `TicketLinks` | One per link of a ticket |
| `TicketBacklog` | The open tickets of each desk at the start of each day, by status, priority, team, assignee, ticket type, channel and category (from the day the breakdown shipped; earlier days show no category), with overdue counts and ages |
| `TicketCategories` | One per ticket category (types that share a taxonomy list it once): its name, parent, level and path, whether customers may choose it, and the desk, team, priority and service-level policy it routes to (its own or inherited). The `CategoryId` of `Tickets` and `TicketBacklog` references it. Only readers with a ticket scope get rows |

The building blocks add their own: `TimelineEntries` (Timeline source) and `ServiceLevelTimers` (Service levels source), whose rows follow each record kind's access. Tickets registers an `IServiceLevelTimerAccessProvider`, so a reader sees the timers of the tickets in their scope.

## Reports

With Reports on, five reports under **Reports** need **View ticket reports** and count only the reader's tickets: **Ticket Volume and Backlog**, **Ticket Response and Resolution Times**, **Ticket Service Level Compliance**, **Ticket Agent and Team Performance** and **Ticket Distribution**. Days are counted in the reader's time zone. **Ticket Service Level Compliance** also has a **By account** table: each account's targets that ended in the period, met and breached, and those running late now, the accounts with the most missed targets first.

With Time Tracker on too, two more reports read the time logged on tickets (`TicketTimeReportsStartup`):

- **Ticket Time**: the time logged in the period, in all, billable and not billed, by person, account, desk and ticket type, and the tickets with the most time. Each entry counts only its part inside the period (a timer still running counts up to now).
- **Ticket Resolution and Work Time**: for the tickets solved in the period, resolution time (calendar time from creation to solved) against work time (how long anyone was working on it, time two people worked at once counted once, time logged before it was opened included) and effort (every person's time added up), the people on each ticket and the touch ratio (the work between its creation and its solving as a share of resolution time, so it never passes 100%), by desk, priority, ticket type and assignee, and the tickets that took the most effort. Only tickets someone logged time on are measured.

A ticket entry counts as not billed when its stage is any desk's **Time not billed to the customer** stage.

**Account Health** (`TicketAccountHealthReport`, under **Executive**) lists each account with tickets, time or targets in the period: tickets opened, solved and open now, the service-level targets that ended in the period and how many were met, and the average satisfaction score of the tickets solved in the period that were rated. With Time Tracker on it adds the time worked on the account's tickets; with Agreements on, and for readers with `ViewAgreements`, the share of the hours of the account's agreements in force today used in their current period (colored from 80%). The accounts with the most open tickets come first.
