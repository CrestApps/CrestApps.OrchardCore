---
sidebar_label: Tickets
title: Tickets
description: "The Tickets help desk: tickets as content items of any ticket type, desks with their own permissions, statuses, priorities and saved views, replies and internal notes on the timeline, team assignment, service-level targets on business hours, e-mails, workflows, webhooks, the audit trail, data sets and reports, the JSON API, the account tab, personal data, AI tools and the recipe and deployment steps."
user_manual: user-manual/administration/tickets
---

**Tickets** is a help desk. A ticket is a content item of a ticket type, filed under a desk (such as IT, HR or customer support) that decides who may see and work on it. Replies, internal notes and every change are kept on the ticket's [timeline](./timeline); first response and resolution targets run on [service levels](./service-levels) counted in [business hours](./business-hours); new tickets go to a team's members through [Teams](./teams).

| Feature | Id | Depends on |
| --- | --- | --- |
| Tickets | `CrestApps.OrchardCore.Tickets` | Timeline, Service Levels, Teams, Resources, Users, Contents, Title |
| Tickets API | `CrestApps.OrchardCore.Tickets.Api` | Tickets |

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.Tickets.Abstractions` | `TicketPart`, `TicketStatus`, `TicketPriority`, `TicketDesk`, `TicketView`, `TicketBacklogDay`, the requests and results of the ticket service, `ITicketService`, `ITicketAccessService`, `ITicketObserver`, `ITicketActionSource`, `ITicketReplyTemplateSource`, `TicketsPermissions` and `TicketsConstants` |
| `CrestApps.OrchardCore.Tickets.Core` | `TicketIndex` and `TicketBacklogIndex`, `DefaultTicketService`, `DefaultTicketAccessService`, `TicketScopeFilter`, `TicketLifecycle`, the auto closer, the backlog recorder, the e-mail observer and template source, the team load source, the timeline, record and service-level timer access, and the API models (`TicketApiModel`, `TicketApiMessages`) |
| `CrestApps.OrchardCore.Tickets` (module) | The pages, the display drivers, the background tasks, the workflow activities, the webhooks and audit trail, the data sets and reports, the API, the account tab, the personal data contributor, the AI tools, the migrations and the recipe and deployment steps |

## Tickets and ticket types

A ticket type is any content type with `TicketPart` (stereotype `Ticket`). The feature creates a `Ticket` type whose title is the **Subject**; add fields to it, or create more types, to ask for different things. Ticket types are not versioned or drafted: a save updates the ticket in place.

The Ticket part's settings on a ticket type (`TicketPartSettings`, edited on the type's Ticket part under **Content Definition**) say what a new ticket of the type starts with: the desk when none is chosen, the team (before the desk's default team), the priority, the service-level policy (used before the desk's) and, with Skills on, the skills an assignee must hold.

`TicketPart` keeps the number, desk, status and its category, priority, requester (a [party](./parties) with a contact snapshot for a guest), account, team, assignee, channel, participants, links (`RecordRef` and a relation), the ticket it was merged into or follows up, and the times and counters reports need: first response, assigned, solved, closed, last customer and agent activity, replies, reopens, reassignments and escalation level. `TicketIndex` serves the list, the views, the reports and the API.

Every change goes through `ITicketService` under a lock per ticket: create, update, reply or note, assign by the desk's strategy, merge, link, edit fields, delete and a list within a scope. Each change made to a ticket is written to its timeline as an event with the field changes, so the ticket's history is never worked out again from its current state.

| Status category | Meaning |
| --- | --- |
| `New`, `Open` | Being worked on |
| `Pending` | Waiting for the requester; a status can pause the service-level timers |
| `OnHold` | Waiting for something else |
| `Solved` | Answered; a reply from the requester within the desk's reopen window reopens it |
| `Closed` | Final and read only; a reply after it opens a follow-up ticket |

## Desks, permissions and scope

A desk keeps its own tickets: every ticket permission exists once over all desks and once for each desk (such as **View all tickets in HR**). Viewing comes with a scope:

| Scope | Permission | Sees |
| --- | --- | --- |
| All | `ViewAllTickets` | Every ticket of the desk |
| My teams | `ViewTeamTickets` | The tickets of the user's teams and those assigned to them |
| Assigned | `ViewAssignedTickets` | The tickets assigned to them |

The other permissions (`EditTickets`, `ReplyToTickets`, `AddTicketNotes`, `AssignTickets`, `MergeTickets`, `DeleteTickets`) apply to the tickets the user may view. `ManageTicketSettings` covers statuses, priorities, desks, shared views and the e-mails; `ViewTicketReports` opens the ticket reports. `TicketScopeFilter` is the one rule the list, the API, the data sets, the reports and the account tab apply, so a user never sees a ticket out of their scope anywhere.

A desk also has its business hours schedule (used by the **Time elapsed** workflow event counted in business hours; service-level targets use their policy's schedule), service-level policy, default team, ticket types, reopen window, auto-close delay, how new tickets are assigned and whether requesters are e-mailed and assignees told.

## Assignment and life cycle

A new ticket with a team and no assignee goes to a member by the desk's **New tickets go to**: nobody, the member with the fewest open tickets (`LeastLoaded`) or each member in turn (`RoundRobin`). A desk chooses which availability checks count under **A team member can take tickets now when**: on a published shift (`WorkforceShifts`), not out of office in the calendar (`CalendarAbsences`) and signed in to Contact Center (`ContactCenterPresence`). A desk that never chose honors shifts and absences but not Contact Center presence, so people who work tickets without signing in to the phone still get them (`TicketDesk.AvailabilitySources`, `null` for those defaults). When nobody can take the ticket (no one available, no one with the type's required skills, or the team has no members who can sign in), it stays unassigned and its timeline says why. Team load is counted in the database. The **Close solved tickets** background task (every 15 minutes) closes solved tickets once the desk's delay has passed; a ticket that can't be closed is logged and skipped, and the run moves on to the rest of the desk. The **Record the ticket backlog** task (every hour) records each desk's open tickets once a day, in the site's time zone, for the backlog reports. It records under a tenant lock and commits before releasing it, so a second run or another node never records a desk's day twice; a run later in the day, after earlier ones failed, still records that day. A day the site never ran at all can't be recorded afterwards (the backlog of a past moment can't be read back) and shows as a gap.

A staff member who leaves, or whose user is unlinked from their staff record, hands back their open tickets: each is unassigned, with the change on its timeline.

## Service levels

Tickets is a [service-level](./service-levels) scope named `Tickets`, with the metrics `FirstResponse`, `NextResponse` and `Resolution` and the priorities by name. A new ticket takes the policy of its account, then its type, then its desk. The first public reply from an agent completes the first response; each customer reply starts a next response; solving completes the resolution; a status that pauses timers pauses them. `TicketPart.NextDueUtc` keeps the next due time for the list and the reports.

## Notifications and e-mails

With a desk that e-mails requesters, the requester gets the `ticket-received`, `ticket-reply` (public replies only) and `ticket-solved` [templated e-mails](./templated-emails), which the site can rewrite under **Settings > Tickets > E-mails**. A template has `Ticket` (`Number`, `Subject`, `Status`, `RequesterName`) and, in a reply, `Reply`: `Reply.Body` is the plain text, encoded where it is written, and `Reply.Html` is the reply already encoded as paragraphs with links to its files, written raw as `{{ Reply.Html | raw }}`. A reply's files are linked with signed download links that work for 30 days, from the site's **Base URL** (without one, the e-mail names the files and asks the requester to open the request).

Every requester e-mail goes through an outbox: it is stored, tried at once, and when it fails tried again after 5 minutes, 15 minutes, an hour, 4 hours and 12 hours, then given up. While a reply's e-mail hasn't reached the requester, the reply on the ticket shows a "not delivered" notice with the reason (the timeline's `Notice` entry property), and the notice goes away once it is sent. A ticket created with `TicketCreateRequest.IsImport` (a bulk import of past tickets) tells no observer, so no requester is e-mailed, no workflow starts and no webhook is sent; tickets brought in by a recipe's `Content` step are saved as content items and tell no observer either. With Notifications on, the assignee is told through Orchard Core notifications when a ticket is given to them (never about one they took). With Omnichannel Messaging on, reply templates of the `Tickets` scope are offered in the composer, filled in for the ticket, and can also set a status, priority or team.

## Workflows

With Workflows on, these events start workflows. Each can be narrowed to desks; the ticket's values are in `Workflow.Input` (`TicketId`, `Number`, `Subject`, `DeskId`, `StatusId`, `StatusCategory`, `PriorityId`, `TeamId`, `AssigneeId`, `Requester`, `ContentType`) with what the event adds (`ChangedFields`, `Previous`, `Body`, `Metric`, `MergedIntoId`, `ActionName`).

| Event | When |
| --- | --- |
| Ticket Created, Ticket Updated (with a changed-field filter), Ticket Status Changed (with a category filter), Ticket Assigned | The ticket changes |
| Ticket Customer Replied, Ticket Agent Replied, Ticket Note Added | An entry is added |
| Ticket Service Level Warning, Ticket Service Level Breached | A target is close to or past due |
| Ticket Solved, Ticket Reopened, Ticket Closed, Ticket Merged | The life cycle moves |
| Ticket Action Requested | An agent runs a named action from the ticket's **Actions** menu |
| Ticket Time Elapsed | A time passes, in clock or business hours, since creation, the last customer or agent reply, or the last status change |

Tasks: Create Ticket, Update Ticket, Assign Ticket, Add Ticket Reply, Add Ticket Note, Escalate Ticket, Get Ticket, Find Tickets, Check Ticket Service Level, Link Tickets and Notify about a Ticket. Changes a workflow makes are marked, and a chain of workflow-caused changes stops starting workflows at depth 3, so rules can't loop.

## Webhooks and audit trail

With Webhooks on, subscribers get `ticket.created`, `ticket.updated`, `ticket.assigned`, `ticket.reply_added` (public replies only; internal notes never leave the site), `ticket.solved` and `ticket.closed`, with the ticket as the API gives it. With Audit Trail on, ticket created, changed, assigned, status changed, merged and deleted are recorded with who did it.

## Data sets

With Data Pipelines Core on, the **Tickets** data source serves the [Report Builder](./report-builder/data-sources) and data pipelines. Every data set shows only the tickets in the reader's scope.

| Data set | Rows |
| --- | --- |
| `Tickets` | One per ticket as it is now: desk, status and priority with their names, requester, account, team, assignee, channel, times, minutes to first response and to solve, replies, reopens, reassignments, escalation level, links and participants. `TicketId` references the `Ticket` type in `Contents`, for its custom fields |
| `TicketEntries` | One per public reply and internal note |
| `TicketChanges` | One per recorded field change: the field, its value before and after, who and when |
| `TicketLinks` | One per link of a ticket |
| `TicketBacklog` | The open tickets of each desk at the start of each day, by status, priority, team, assignee, ticket type and channel, with overdue counts and ages |

The building blocks add their own: `TimelineEntries` (Timeline source) and `ServiceLevelTimers` (Service levels source), whose rows follow each record kind's access. Tickets registers an `IServiceLevelTimerAccessProvider`, so a reader sees the timers of the tickets in their scope.

## Reports

With Reports on, five reports under **Reports** need **View ticket reports** and count only the reader's tickets: **Ticket Volume and Backlog**, **Ticket Response and Resolution Times**, **Ticket Service Level Compliance**, **Ticket Agent and Team Performance** and **Ticket Distribution**. Days are counted in the reader's time zone.

## API

The **Tickets API** feature maps `api/tickets/v1` with Orchard Core's `Api` authentication scheme (a bearer token). Each call checks the caller's ticket permissions and scope as the pages do; a ticket out of scope is answered `404`. Writes take an `Idempotency-Key` header, so sending one again returns the first answer.

| Endpoint | Does | Needs |
| --- | --- | --- |
| `GET tickets` | Lists tickets, newest change first: `q` (number or subject), `deskId`, `status` (categories, comma separated), `assignee` (`me`, `unassigned` or a user id), `requester`, `accountId`, `skip`, `take` (at most 100) | A view scope |
| `GET tickets/{id}` | A ticket by its id or number | View |
| `GET tickets/{id}/entries` | Its replies, notes and events, newest first, with download addresses for files that work for an hour | View |
| `POST tickets` | Opens a ticket: `type`, `subject`, `body`, `deskId`, `priorityId`, `requester` (`Kind:Id`) or `requesterName` and `requesterEmail`, `accountId`, `attachments` | Reply on the desk |
| `PATCH tickets/{id}` | Changes `subject`, `statusId`, `priorityId`, `deskId`, `escalationLevel` (edit) and `teamId`, `assigneeId` (assign) | Edit or assign |
| `POST tickets/{id}/replies` | A public reply with `body`, `setStatusId` and `attachments` | Reply |
| `POST tickets/{id}/notes` | An internal note with `body` and `attachments` | Note |

Files travel as `{ "fileName", "contentType", "content" }` with the content in base64, under the site's [attachment](./attachments) policy. Tickets opened through the API have the `Api` channel.

## Accounts, personal data and AI tools

With Accounts on, an account's page has a **Tickets** tab with its latest tickets in the user's scope and **New ticket** for the account.

Tickets takes part in [personal data](./personal-data) requests: a person's tickets are found by their user and by the e-mail kept on a guest's ticket. An export lists each ticket and its public replies; an erasure removes the contact details, rewrites the subject and rewrites the text of the ticket's timeline (`ITimelineService.ScrubAsync`) so nothing names them.

With AI on, these tools can be picked on AI profiles (category **Tickets**). Each acts as the signed-in user, within their scope and permissions.

| Tool | Does |
| --- | --- |
| `listTicketOptions` | Lists the desks the user may open tickets in, the statuses and the priorities, with their ids |
| `searchTickets` | Finds tickets by number or subject, status category, desk and assignee |
| `getTicket` | Reads a ticket and its latest 20 entries |
| `createTicket` | Opens a ticket on a desk |
| `addTicketReply` | Sends a public reply or adds an internal note |
| `updateTicket` | Changes the status, priority, team or assignee |

## Recipes and deployment

| Step | Holds |
| --- | --- |
| `TicketStatuses` | Statuses |
| `TicketPriorities` | Priorities |
| `TicketDesks` | Desks, matched by `ItemId` and then by name |
| `TicketViews` | Saved views; a personal view carries its owner's user name (`OwnerUserName`), resolved to the user of the target site, and a view whose owner isn't a user there is refused with that name |

Each has a deployment step and schemas for CrestApps Recipes. A record whose `ItemId` isn't a valid id (26 letters and digits) is refused with its name and id rather than imported under a new id, which would cut the links other records make to it. A status's or priority's color must be a hex color or a color name, in the editor and in a recipe. Tickets themselves are content items and travel with Orchard Core's content step; their entries with the `TimelineEntries` step. The backlog snapshots are history the site records and have no step.

The **Help desk starter** recipe sets up weekday business hours, a `Tickets` service-level policy, the Support desk and an IT desk, and two example workflows that start turned off: one escalates a ticket whose target is breached and says why in a note, the other adds a note when a requester reopens a solved ticket.

## Extending Tickets

| To | Use |
| --- | --- |
| React to ticket changes | `ITicketObserver` (created, updated, assigned, status changed, replied, merged, deleted, service level) |
| Offer actions on the ticket page | `ITicketActionSource` |
| Offer reply templates in the composer | `ITicketReplyTemplateSource` |
| Add a panel to the ticket page | A `ContentPartDisplayDriver<TicketPart>` or a display driver of the ticket type |
| Read or change tickets | `ITicketService` and `ITicketAccessService`; apply `TicketScopeFilter` to your own ticket queries |
