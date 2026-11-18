---
sidebar_label: Assignment and life cycle
title: Assignment and life cycle
description: "How tickets change: the ticket service and its lock, assignment by a desk's strategy and availability, the background tasks that close tickets and record the backlog, planning values, presence on an open ticket, and problems with their incidents."
user_manual:
  - user-manual/service-desk/answer-a-ticket
  - user-manual/service-desk/merge-and-link
  - user-manual/service-desk/desks
---

## The ticket service

`TicketPart` keeps the number, desk, status and its category, priority, requester (a [party](../parties) with a contact snapshot for a guest), account, team, assignee, channel, participants, links (`RecordRef` and a relation), the ticket it was merged into or follows up, and the times and counters reports need: first response, assigned, solved, closed, last customer and agent activity, replies, reopens, reassignments and escalation level. `TicketIndex` serves the list, the views, the reports and the API.

Every change goes through `ITicketService` under a lock per ticket: create, update, reply or note, assign by the desk's strategy, merge, link, edit fields, delete and a list within a scope. Each change made to a ticket is written to its timeline as an event with the field changes, so the ticket's history is never worked out again from its current state. The meaning of each status category, including the reopen window of a solved ticket and the follow-up a reply to a closed one opens, is in [Desks and settings records](./desks-and-settings#statuses).

## Assignment

A new ticket with a team and no assignee goes to a member by the desk's **New tickets go to**: nobody, the member with the fewest open tickets (`LeastLoaded`) or each member in turn (`RoundRobin`). A desk chooses which availability checks count under **A team member can take tickets now when**: on a published shift (`WorkforceShifts`), not out of office in the calendar (`CalendarAbsences`) and signed in to Contact Center (`ContactCenterPresence`). A desk that never chose honors shifts and absences but not Contact Center presence, so people who work tickets without signing in to the phone still get them (`TicketDesk.AvailabilitySources`, `null` for those defaults). When nobody can take the ticket (no one available, no one with the type's required skills, or the team has no members who can sign in), it stays unassigned and its timeline says why. Team load is counted in the database.

A staff member who leaves, or whose user is unlinked from their staff record, hands back their open tickets: each is unassigned, with the change on its timeline.

## Background tasks

The **Close solved tickets** background task (every 15 minutes) closes solved tickets once the desk's delay has passed; a ticket that can't be closed is logged and skipped, and the run moves on to the rest of the desk. The **Record the ticket backlog** task (every hour) records each desk's open tickets once a day, in the site's time zone, for the backlog reports. It records under a tenant lock and commits before releasing it, so a second run or another node never records a desk's day twice; a run later in the day, after earlier ones failed, still records that day. A day the site never ran at all can't be recorded afterwards (the backlog of a past moment can't be read back) and shows as a gap.

## Planning values

A ticket also carries planning values, on the **Planning** panel beside it (`TicketPart.Impact`, `Urgency`, `EstimatedStartUtc`, `EstimatedMinutes`, `BillingType`, changed through `TicketUpdate` with `SetPlanning` or `SetEstimate`, each change on the timeline with change keys `Impact`, `Urgency`, `Estimate` and `BillingType`). A desk's priority matrix turns impact and urgency into a priority (see [Desks and settings records](./desks-and-settings#priorities-and-the-priority-matrix)). The person who last solved a ticket is kept in `TicketPart.SolvedById` and the ticket index, for reports. The API returns all of them.

## Presence on an open ticket

An open ticket page checks in every 15 seconds (`POST Admin/ticket/{id}/presence`, antiforgery, the ticket's view permission). `TicketPresence` keeps who has the ticket open, and whether they have text in the composer, in the distributed cache, so every node sees the same people; someone who stopped checking in for 45 seconds, or closed the page, is gone. The page shows the others ("Ana is writing on this ticket", "Also viewing: Ben") and, when the ticket's last change, reply count, status or assignee differ from the ones it was loaded with (`TicketPresenceVersion`), says the ticket changed and offers to reload. Nothing is pushed: a change shows on the next check-in.

## Problems and incidents

A ticket linked to another with **Its problem** (`TicketLinkKinds.Problem`) is an incident of that problem. Ticket-to-ticket links are indexed both ways (`TicketLinkIndex`), so the problem's page lists its incidents, and solving the problem solves its open incidents with the same status, one by one through the ticket service (each under its own lock, with its events and the requester's "solved" e-mail), whatever desk they are on (`TicketProblemObserver`). The agent who solved the problem is recorded on each, with "Solved with its problem T-…" as the reason. The request solves the first 25; the **Solve incidents of solved problems** background task (every 5 minutes, up to 200 a run) does the rest. Linking needs the agent to see both tickets, and the problem's list names only the incidents the viewer can see (the others are counted). An incident already solved, merged or only related stays as it is.
