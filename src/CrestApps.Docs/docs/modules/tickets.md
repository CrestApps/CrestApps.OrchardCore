---
sidebar_label: Tickets
title: Tickets
description: "The Tickets help desk: tickets as content items of any ticket type, desks with their own permissions, statuses, priorities, saved views and boards, replies and internal notes on the timeline, team assignment, service-level targets on business hours, e-mails, workflows, webhooks, the audit trail, data sets and reports, the JSON API, the account tab, personal data, AI tools and the recipe and deployment steps."
user_manual: user-manual/administration/tickets
---

**Tickets** is a help desk. A ticket is a content item of a ticket type, filed under a desk (such as IT, HR or customer support) that decides who may see and work on it. The admin menu is **Service Desk**: **Tickets** and **Boards**, and for those who manage it, **Desks**, **Statuses**, **Priorities**, **Views** and **Notification templates**. A ticket's page shows its **Conversation** (replies and internal notes, `Reply` and `Note` entries) apart from its **History** (every change: status, assignee, priority, links and the rest, `Event` entries), each paged on its own. Replies, internal notes and every change are kept on the ticket's [timeline](./timeline); first response and resolution targets run on [service levels](./service-levels) counted in [business hours](./business-hours); new tickets go to a team's members through [Teams](./teams).

| Feature | Id | Depends on |
| --- | --- | --- |
| Tickets | `CrestApps.OrchardCore.Tickets` | Timeline, Service Levels, Teams, Resources, Users, Contents, Title, Conditional Fields |
| Tickets API | `CrestApps.OrchardCore.Tickets.Api` | Tickets |
| Tickets Portal | `CrestApps.OrchardCore.Tickets.Portal` | Tickets, My Account, Rate Limits |
| Ticket Web Form | `CrestApps.OrchardCore.Tickets.WebForm` | Tickets, Rate Limits |
| Ticket Approvals | `CrestApps.OrchardCore.Tickets.Approvals` | Tickets |
| Tickets for Contact Center | `CrestApps.OrchardCore.Tickets.ContactCenter` (its own module) | Tickets, Contact Center Work Distribution |

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.Tickets.Abstractions` | `TicketPart`, `TicketStatus`, `TicketPriority`, `TicketDesk`, `TicketView`, `TicketBoard`, `TicketBacklogDay`, the requests and results of the ticket service, `ITicketService`, `ITicketAccessService`, `ITicketBoardService`, `ITicketObserver`, `ITicketActionSource`, `ITicketReplyTemplateSource`, `TicketsPermissions` and `TicketsConstants` |
| `CrestApps.OrchardCore.Tickets.Core` | `TicketIndex` and `TicketBacklogIndex`, `DefaultTicketService`, `DefaultTicketAccessService`, `DefaultTicketBoardService`, `TicketBoardColumns`, `TicketScopeFilter`, `TicketLifecycle`, the auto closer, the backlog recorder, the e-mail observer and template source, the team load source, the timeline, record and service-level timer access, and the API models (`TicketApiModel`, `TicketApiMessages`) |
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

## Categories and the questions that follow them

A ticket type can take its **categories** from an Orchard Core taxonomy (Hardware > Laptop > Battery): the Ticket part's settings name the taxonomy (`TicketPartSettings.CategoryTaxonomyId`), whether a category is required on the forms (`CategoryRequired`, agents' page, portal, web form; never for the API, workflows or e-mail) and whether only a category without sub-categories may be chosen (`CategoryLeavesOnly`). Several types may share a taxonomy. The Tickets feature defines a `TicketCategory` term type with the `TicketCategoryPart`; any taxonomy whose terms carry the part works.

`TicketCategoryPart` (on the term, edited in the taxonomy editor) gives a category's tickets a desk, team, priority and service-level policy, and says whether customers can choose it (`HiddenFromCustomers`) and what they read about it (`CustomerDescription`). A category takes each value it leaves empty from the nearest category above it, and a category hidden from customers hides those below it. `ITicketCategoryService` (`ListAsync`, `FindAsync`) reads a type's categories as `TicketCategory` records with their inherited values, `Path` (`/top/child/category/`) and `PathText` ("Hardware > Laptop").

A new ticket keeps `TicketPart.CategoryId` and `CategoryPath`. What the request says comes first, then the category, then the ticket type, then the desk; the service-level policy is the account's, then the category's, the type's and the desk's. A desk the request only suggests (`TicketCreateRequest.DeskIsSuggestion`, as the portal and the web form send it) gives way to the category's desk when that desk serves the same audience and takes the type, so a customer's choice never moves their request to an internal desk. The category's team is used only when the ticket lands on the category's desk (or the category names none).

A ticket keeps its category's path (`CategoryPath`, the ids from the top category down) for the list's "with sub-categories" filter and the reports. When a category moves under another parent, publishing the taxonomy asks for the resumable **Tickets.CategoryPaths** step (`TicketCategoryPathsStep`), which brings the paths of the tickets up to date a page at a time; renaming a category or changing its routing changes no path. Changing a ticket's category (`TicketUpdate.CategoryId`, an empty string clears it) changes only the category, recorded on the timeline with the change key `Category`. When the category's routing differs from the ticket's, the properties panel offers **Apply the category's routing** (`Admin/ticket/{id}/category-routing`), which moves the desk (a desk the agent may move tickets to), the priority and, with the assign permission, the team, through the same checks as a manual change. A workflow on Ticket Updated with the `Category` change key can do it instead.

**Questions that follow the answers.** The Tickets feature depends on **Conditional Fields** (Content Fields module), so every field of a ticket type can have **Show this field when**, a rule on its other fields and on the category: Tickets offers the category to rules as the `TicketCategory` source (`IContentConditionSource`), with the categories above it, so "is or is under Hardware" covers its sub-categories. The forms put the chosen category on the new ticket before its editor is read, so the server applies the rules to what was chosen, and the picker posts `CategoryId`, which the rules' script reads in the browser. On a customer's form, the fields customers don't fill in are hidden fields too (`TicketCustomerFieldsVisibility`, an `IContentFieldVisibilityProvider`): what was posted for them is dropped and their validation is ignored, so a required internal field never refuses a customer's request.

The category picker is the Content Fields `TaxonomyCascadingPicker` shape: one list per level (Category, then Sub-category), with the category's description for customers under it.

| Where | What it does with categories |
| --- | --- |
| Ticket list | **Category** picker (a category and those below it, `TicketQuery.CategoryId` matching `TicketIndex.CategoryPath`), kept on the pager's links |
| Index | `TicketIndex.CategoryId` and `CategoryPath` (Tickets migration v12) |
| API | `GET tickets?categoryId=`, `POST tickets` and `PATCH tickets/{id}` take `categoryId`; `TicketApiModel` has `categoryId` and `categoryPath` (webhooks too) |
| Workflows | `CategoryId` and `CategoryPath` in `Workflow.Input`; **Update Ticket** sets the category; Ticket Updated's changed-field filter knows `Category` |
| Data sets | `Tickets`: `CategoryId`, `Category`, `CategoryPathText`, `CategoryLevel1`, `CategoryLevel2` |
| Recipes | Categories are a taxonomy content item (the `Content` step); the type's settings and the field rules travel with `ContentDefinition`. The **Help desk starter** recipe adds an **IT categories** taxonomy and an **IT request** type with questions that follow the category and an outage answer |

A term that is deleted while tickets use it leaves them with its id; they show "Removed category". A category moved inside the taxonomy keeps its tickets' old paths until they are saved again.

## Desks, permissions and scope

A desk keeps its own tickets: every ticket permission exists once over all desks and once for each desk (such as **View all tickets in HR**). Viewing comes with a scope:

| Scope | Permission | Sees |
| --- | --- | --- |
| All | `ViewAllTickets` | Every ticket of the desk |
| My teams | `ViewTeamTickets` | The tickets of the user's teams and those assigned to them |
| Assigned | `ViewAssignedTickets` | The tickets assigned to them |

The other permissions (`EditTickets`, `ReplyToTickets`, `AddTicketNotes`, `AssignTickets`, `MergeTickets`, `DeleteTickets`) apply to the tickets the user may view. `ManageTicketSettings` covers statuses, priorities, desks, shared views and the e-mails (and implies `ManageTicketBoards`); `ViewTicketReports` opens the ticket reports. `TicketScopeFilter` is the one rule the list, the API, the data sets, the reports and the account tab apply, so a user never sees a ticket out of their scope anywhere.

A desk also has its business hours schedule (used by the **Time elapsed** workflow event counted in business hours; service-level targets use their policy's schedule), service-level policy, default team, ticket types, reopen window, auto-close delay, how new tickets are assigned and whether requesters are e-mailed and assignees told.

## Boards

A board (`TicketBoard`, **Service Desk → Boards**, `Admin/tickets/boards`) shows tickets as cards in columns. Each column (`TicketBoardColumn`) holds one or more statuses, such as a **Pending** column for **Waiting for the requester** and **Waiting for a third party**; a status is in one column of a board only, and tickets in a status that is in no column don't show on that board. Dragging a card to another column (or choosing **Move** on the card, for the keyboard and touch screens) sets the ticket's status: to the column's only status, to its **default** when it has several, or to the one the agent picks when it has several and no default. A column can have a limit; the header turns red when the column holds more, and moves are never refused for it.

A board keeps a `TicketQuery` for which tickets it shows (desks, teams, ticket types, assignee and the order of the cards; the columns decide the statuses) and is shared with everybody, with a team, or kept by its owner. Whoever opens it sees only the tickets their desk scope covers: `DefaultTicketBoardService.LoadAsync` runs one query per column through `ITicketAccessService.ApplyScopeAsync`, at most 50 cards a column with the column's total. The board page (`Admin/tickets/board/{id}`) loads its cards from `Admin/tickets/board/{id}/data` (JSON) and again when tickets change: with SignalR on, `TicketBoardNotifier` (a ticket observer) pings the open boards through `TicketBoardHub` once a change is saved (one ping per request, carrying nothing about the ticket; each board reloads what its reader may see, and only users with a desk view scope may join), and a board that isn't connected loads again every 20 seconds while it is visible, so a card moves when the ticket's status changes elsewhere; its search and **My tickets** / **Unassigned tickets** filter stay in the address.

A move (`POST Admin/tickets/board/{id}/move`, antiforgery) goes through `ITicketBoardService.MoveAsync`: the user needs the Edit permission on the ticket's desk, the ticket must still be in the status the board showed (otherwise the move is refused and the board reloads), and the status is set by `ITicketService.UpdateAsync` with the reason "Moved on the board X" on the timeline, so the life cycle, approval holds, observers, e-mails and workflows apply as on the ticket page. A refusal comes back with the ticket service's reason and the card goes back; a closed ticket's card can't be dragged.

| Permission | Lets a user |
| --- | --- |
| `ManageTicketBoards` (**Manage shared ticket boards**) | Create, arrange and delete boards shared with everybody or a team |
| `ManageOwnTicketBoards` (**Manage my own ticket boards**) | Create, arrange and delete their own, personal boards |

Opening a board needs no board permission, only the right to view tickets on some desk. Nobody arranges another user's personal board. Deleting a status doesn't touch boards: a column skips statuses that no longer exist, and its editor drops them. The feature adds a **Service desk** board, with a column per status category (closed left out), to a site that has none.


Each user may pin boards to their own admin menu (**Pin to menu** on the board and in the **Boards** list): `ITicketBoardPins` keeps them on the user record (`TicketBoardPinsPart` in the user's properties), and `TicketsAdminMenu` adds each pinned board the user may still see under **Service Desk**, after **Boards**, in the order pinned.

## Assignment and life cycle

A new ticket with a team and no assignee goes to a member by the desk's **New tickets go to**: nobody, the member with the fewest open tickets (`LeastLoaded`) or each member in turn (`RoundRobin`). A desk chooses which availability checks count under **A team member can take tickets now when**: on a published shift (`WorkforceShifts`), not out of office in the calendar (`CalendarAbsences`) and signed in to Contact Center (`ContactCenterPresence`). A desk that never chose honors shifts and absences but not Contact Center presence, so people who work tickets without signing in to the phone still get them (`TicketDesk.AvailabilitySources`, `null` for those defaults). When nobody can take the ticket (no one available, no one with the type's required skills, or the team has no members who can sign in), it stays unassigned and its timeline says why. Team load is counted in the database. The **Close solved tickets** background task (every 15 minutes) closes solved tickets once the desk's delay has passed; a ticket that can't be closed is logged and skipped, and the run moves on to the rest of the desk. The **Record the ticket backlog** task (every hour) records each desk's open tickets once a day, in the site's time zone, for the backlog reports. It records under a tenant lock and commits before releasing it, so a second run or another node never records a desk's day twice; a run later in the day, after earlier ones failed, still records that day. A day the site never ran at all can't be recorded afterwards (the backlog of a past moment can't be read back) and shows as a gap.

A ticket also carries planning values, on the **Planning** panel beside it (`TicketPart.Impact`, `Urgency`, `EstimatedStartUtc`, `EstimatedMinutes`, `BillingType`, changed through `TicketUpdate` with `SetPlanning` or `SetEstimate`, each change on the timeline with change keys `Impact`, `Urgency`, `Estimate` and `BillingType`). A desk's **Priority matrix** (`TicketDesk.PriorityMatrix`, in the desk recipe step) gives the priority of each impact and urgency: when an update changes either and names no priority, or a new ticket has both and its request names no priority, the ticket gets the priority of its cell; a cell left empty leaves the priority alone. The person who last solved a ticket is kept in `TicketPart.SolvedById` and the ticket index, for reports. The API returns all of them.

A staff member who leaves, or whose user is unlinked from their staff record, hands back their open tickets: each is unassigned, with the change on its timeline.

An open ticket page checks in every 15 seconds (`POST Admin/ticket/{id}/presence`, antiforgery, the ticket's view permission). `TicketPresence` keeps who has the ticket open, and whether they have text in the composer, in the distributed cache, so every node sees the same people; someone who stopped checking in for 45 seconds, or closed the page, is gone. The page shows the others ("Ana is writing on this ticket", "Also viewing: Ben") and, when the ticket's last change, reply count, status or assignee differ from the ones it was loaded with (`TicketPresenceVersion`), says the ticket changed and offers to reload. Nothing is pushed: a change shows on the next check-in.

## Problems and incidents

A ticket linked to another with **Its problem** (`TicketLinkKinds.Problem`) is an incident of that problem. Ticket-to-ticket links are indexed both ways (`TicketLinkIndex`), so the problem's page lists its incidents, and solving the problem solves its open incidents with the same status, one by one through the ticket service (each under its own lock, with its events and the requester's "solved" e-mail), whatever desk they are on (`TicketProblemObserver`). The agent who solved the problem is recorded on each, with "Solved with its problem T-…" as the reason. The request solves the first 25; the **Solve incidents of solved problems** background task (every 5 minutes, up to 200 a run) does the rest. Linking needs the agent to see both tickets, and the problem's list names only the incidents the viewer can see (the others are counted). An incident already solved, merged or only related stays as it is.

## Work orders

With Work Orders on, the ticket page has a **Work order** panel for agents who may change the ticket and hold **Manage work orders**: they choose a work order type and, optionally, a priority, and `TicketWorkOrders` creates the work order through `WorkOrderService` (the type's defaults, number and service level apply). The work order takes the ticket's subject as its summary (shortened to 120 characters), the first message as its description, the ticket's account, the requester (the contact when the requester is one, with name, e-mail, phone and address) and `Source` `Ticket` with the ticket's id as `SourceId`; the ticket links to it (**Related**), so it shows under **Links**. When the work order is completed or canceled, `TicketWorkOrderObserver` adds an internal event to the ticket's timeline (with the cancel reason); the ticket's status is left to the agent. The **Create Work Order From Ticket** workflow task does the same from a workflow, with the type by name or id, and sets the `WorkOrderId` and `WorkOrderNumber` outputs. This is an integration of the Tickets feature (`[RequireFeatures]`), not a feature of its own.

## Contact Center routing

The **Tickets for Contact Center** module (`CrestApps.OrchardCore.Tickets.ContactCenter`) lets a desk offer its tickets through a Contact Center queue, so the queue's presence, capacity and skills routing decide who gets them. A desk's editor gets **Contact Center queue** (`TicketDeskQueueSettings`, in the desk's properties). A new ticket on that desk that nobody has (and that isn't waiting for approval) becomes a task activity (`ActivityKind.Task`, source `Api`, the ticket's number and subject as its instructions, the ticket's id in `TicketActivityLink`) enqueued with the ticket's priority (`IActivityQueueService.EnqueueAsync`); the ticket links to the activity (`OmnichannelActivity:{id}`), which shows on its timeline. When an agent accepts the offer (`QueueItemAssigned`), `TicketOfferAcceptedHandler` gives them the ticket, unless someone already has it; replaying the event changes nothing. When the ticket is given to someone by hand, solved, closed, merged or deleted before an agent took it, the queued work is withdrawn (`IQueuedWorkWithdrawalService`); work an agent already took stays theirs to complete in the agent workspace, like any activity. Agents accept offers in the agent desktop, which needs the Contact Center Voice and real-time features.

## Time on tickets

With Time Tracker on, a desk's editor has **Time on tickets**: the Time Tracker stage its agents' ticket work is recorded in (`TicketDeskTimeSettings.StageId`, in the desk's properties). The ticket page then has a **Time** panel. **Start working on this ticket** switches the agent's time to that stage for the ticket (`ITimeTrackerService.SwitchAsync` with `Source` `Ticket` and `SourceRef` `Ticket:{id}`), so time on tickets is part of the person's day, with no second ledger; starting another ticket ends the first entry. **Stop working on this ticket** goes back to the stage the agent was in before they started on tickets, or clocks them out if they started on the ticket. The panel adds up each person's time on the ticket from the entries for it (`TicketTime.SummarizeAsync`, open entries up to now); people whose time another clock keeps can't start from a ticket, as with any switch. Time entries are corrected in Time Tracker. This is an integration of the Tickets feature (`[RequireFeatures]`), not a feature of its own.

**Log time** on the panel records time spent after the fact (`TicketTime.LogAsync` with a `TicketTimeLog`): the time spent as people write it (`30`, `45m`, `1h 30m`, `1:30`, `1.5h`, read by `TicketTimeDuration.ParseMinutes`, up to 24 hours), when it started in the agent's time zone (empty when the work just ended), a note and whether it is billable. The entry goes through `ITimeTrackerService.InsertAsync`, so it takes its place in the agent's day: the time it covers, such as their Work, is cut around it and their total doesn't change. Another ticket's time and time another clock keeps are never cut; logging over them is refused. A desk may also name **Time not billed to the customer** (`TicketDeskTimeSettings.NotBilledStageId`), a second stage for time logged as not billable; give each stage the time categories it counts toward (Billable only on the first), so Time Tracker's own reports agree with the ticket reports. The panel lists the ticket's entries with their notes and what wasn't billed, and links users with **Edit anyone's time** to the time entries page, where entries are corrected or removed.

## Agreements

With [Agreements](./agreements.md) on, a ticket works under an agreement with its account (`TicketPart.AgreementId`, in the index). An agreement covers tickets when it has `TicketAgreementTerms` (its **Tickets** panel): the desks and ticket types it covers (none for all) and the service-level policy of its tickets. A new ticket with an account takes the earliest agreement in force whose terms cover its desk and type (`ITicketAgreementResolver`), unless the request names one of the account's agreements (`TicketCreateRequest.AgreementId`); the agreement's policy comes before the account's entitlement, the category's, the type's and the desk's. Agents change it under **Planning** (`TicketUpdate.AgreementId`, empty to clear), with change key `Agreement` on the timeline; another account's agreement is refused, and a new agreement's policy retargets the ticket's timers. With Time Tracker on, the time logged on the agreement's tickets counts against its hours (`TicketAgreementUsageProvider`): the time in a desk's ticket stage, not in its stage for time not billed, on tickets whose billing type isn't not billable or no charge. Each time entry is charged to the agreement its ticket works under when the time is logged (`TicketTimeChargeResolver`, Time Tracker's `TimeEntry.ChargedTo`), so changing a ticket's agreement moves only the time logged after the change; the time before stays on the old agreement's hours, balance and reports. Time logged before entries remembered their agreement was charged, on upgrade, to the agreement its ticket had then. The ticket's page shows its agreement with the hours left to those who may view agreements.

## Service levels

With [Accounts](./accounts) on, an account's **Ticket service level** (`TicketAccountPart`, on the `Account` type) names the policy its tickets run on, before the ticket type's and the desk's, for a premium support plan, say. With **Account E-mail Domains** on, a new ticket without an account is filed under the account whose domain its requester's e-mail is on, unless the address is unconfirmed (a guest's on the web form), since anyone could type a customer's address.


Tickets is a [service-level](./service-levels) scope named `Tickets`, with the metrics `FirstResponse`, `NextResponse` and `Resolution` and the priorities by name. A new ticket takes the policy of its account, then its type, then its desk. The first public reply from an agent completes the first response; each customer reply starts a next response; solving completes the resolution; a status that pauses timers pauses them. `TicketPart.NextDueUtc` keeps the next due time for the list and the reports.

## Notifications and e-mails

With a desk that notifies requesters, the requester gets the `ticket-received`, `ticket-reply` (public replies only), `ticket-solved` and `ticket-rating` messages, [templated](./templated-emails) so the site can rewrite them under **Service Desk > Notification templates**. A requester who has a site account gets each through Orchard Core notifications, as the `TicketRequestReceived`, `TicketReplied`, `TicketRequestSolved` and `TicketRatingRequested` notification types (`TicketRequesterNotifier`, an `ITicketRequesterNotifier`): the message the template wrote is the notification's subject and body, it opens the request in their account, and they get it in their notification center and by the methods they choose, or turn a kind off. A requester without an account (a contact or a guest), or one whose account is gone, or any requester while the Notifications feature is off, gets the same message by e-mail (`TicketEmailOutboxItem.RecipientUserId` names the account, `To` the address). A template has `Ticket` (`Number`, `Subject`, `Status`, `RequesterName`, and `Category`, the category with those above it such as "Hardware > Laptop") and, in a reply, `Reply`: `Reply.Body` is the plain text, encoded where it is written, and `Reply.Html` is the reply already encoded as paragraphs with links to its files, written raw as `{{ Reply.Html | raw }}`. A reply's files are linked with signed download links that work for 30 days, from the site's **Base URL** (without one, the e-mail names the files and asks the requester to open the request).

Every requester message goes through an outbox: it is stored, tried at once, and when it fails tried again after 5 minutes, 15 minutes, an hour, 4 hours and 12 hours, then given up. While a reply's e-mail hasn't reached the requester, the reply on the ticket shows a "not delivered" notice with the reason (the timeline's `Notice` entry property), and the notice goes away once it is sent. An e-mail that is given up is also logged as a warning. A ticket created with `TicketCreateRequest.IsImport` (a bulk import of past tickets) tells no observer, so no requester is e-mailed, no workflow starts and no webhook is sent; tickets brought in by a recipe's `Content` step are saved as content items and tell no observer either. With Notifications on, people are told through Orchard Core notifications, each kind as its own notification type (`TicketsConstants.NotificationTypes`, registered with `AddNotificationType`, so users choose per type and method in their notification preferences): `TicketAssigned` (`TicketAssignmentNotifier`), `TicketTeamAssigned` (the team's members other than the assignee, when a ticket is created with a team or its team changes), `TicketRequesterReplied` (the assignee, or the team's members when nobody has the ticket), `TicketNoteAdded` (the assignee, unless the note mentions them), `TicketStatusChanged` (the assignee; not a new ticket's first status, nor the reopening by the requester's reply), `TicketServiceLevel` (the assignee or the team, once per metric and state while unread), `TicketMerged` (the assignee of the ticket merged into), `TicketWorkflow` (the Notify About Ticket task). `TicketApprovalRequested` comes with Approvals. `TicketNotificationSender` never tells the person who made the change (`TicketEventActor`), and the desk's **Tell the assignee** covers the assignment and team notices. Each notification carries the ticket as its `ReferenceId`, its admin page as its `Url` and `TicketNotificationInfo` (number, subject, desk), which `TicketNotificationDisplayDriver` shows in the navbar and the notification center; `TicketNotificationVisibilityHandler` hides it from a user who may no longer view the ticket (an approver keeps the approval notice, a requester the notices about their own request). With Omnichannel Messaging on, reply templates of the `Tickets` scope are offered in the composer, filled in for the ticket, and can also set a status, priority or team.

## From a messaging conversation

With the [messaging workspace](../omnichannel/messaging-workspace) on, each conversation row has **Open a ticket**. It opens **New ticket** with the customer as requester (their contact, or their address as a name), a subject and the latest 30 messages, oldest first, with who wrote each, as the first message, for an agent who may read the conversation (`TicketConversationSource`). The new ticket links back to the conversation (`MessagingConversation:{id}`, Related).

## Portal

The **Tickets Portal** feature (`CrestApps.OrchardCore.Tickets.Portal`) adds a **Requests** page to the site's [My account](./users#my-account) area:

- `account/requests` lists the signed-in person's requests, newest first, with the status as customers see it. An administrator of an [account](./accounts) (an account member with the Admin role) also gets a tab with the account's requests.
- `account/requests/new` offers the ticket types whose Ticket part settings say **Customers can submit it**, with their description for customers, on the desks that serve customers (and, with [Staff](./staff) on, the internal desks for a person who is a member of staff, so employees file IT and HR requests from the same catalog); then the type's own form: its title (the subject) and only the fields whose settings say **Customers fill this in** (`TicketFieldCustomerSettings`, off by default), then the description and files. Other parts and fields are hidden while the form is built (`TicketCustomerPlacementProvider`) and put back as a new item has them after it is read (`TicketCustomerEditor`), so an internal field on a type customers submit can't be set by posting it. The ticket opens with the `Portal` channel and the person as its requester.
- `account/requests/{number}` shows the public replies, oldest first, with download links for their files that work for an hour, and a reply box. Internal notes and the record of changes never show. A reply from the requester reopens a solved request as a reply on an agent's page would.
- A requester without a login opens `requests/{id}?token=…`, the signed link in their e-mails (90 days), to read and answer that one request. The link is bound to the ticket, its requester (their user, or their e-mail address) and a version (`TicketPart.AccessVersion`): a new requester, a merge or an erasure leaves every earlier link dead. On first use the token is traded for an HttpOnly cookie scoped to that request's path (12 hours) and the page reloads without it, so it leaves the address bar, history and logs. Rating links work the same way.
- Every portal page is sent with `Cache-Control: no-store`, `Referrer-Policy: no-referrer` and `X-Robots-Tag: noindex`. Outcomes (received, reply sent, rating saved, or why not) are written on the page itself, and a refused reply keeps its text, since a site's theme may not show notifications.
- What shows: a ticket on a desk that serves customers to its requester and the administrators of its account; a ticket on an internal desk only to its requester, when they are staff, never through an account. A member of an account (any role) files new requests on customer desks under the account. Requester e-mail links are built from the site's **Base URL** only; without one they are left out and a warning is logged.
- Limits (through Orchard Core's Rate Limits, which the portal turns on): 10 new requests and 30 replies or ratings per signed-in requester in 10 minutes, and 10 guest replies or ratings per address (groups `tickets-portal-create`, `tickets-portal-reply`, `tickets-portal-guest`); 20 portal replies per request an hour; uploads within the attachment limits (checked before a file is read, and a 210 MB request cap). The thread shows the original request and the latest 200 messages, and below it the request's files (the files of its public messages and of the requests of the same requester merged into it), by links signed for an hour.

**Ratings.** Once a request is solved, its requester rates it from 1 to 5 with an optional comment, on the request's page in My account or from the `ticket-rating` e-mail. A desk's **Ask requesters to rate a solved ticket after (hours)** (`TicketDesk.RatingRequestDelayHours`, 0 never asks) sends that e-mail once, through the outbox, to the requester of a ticket solved that long ago and no more than a week before (so turning it on doesn't e-mail every old ticket); its link is signed and works for 30 days (`requests/{id}/rate`), and in its template `Ticket.Link` is the rating page. `ITicketService.RateAsync` keeps one rating per ticket (a second replaces it) on `TicketPart.Satisfaction`, the index (`SatisfactionScore`), the timeline ("Rated 4 out of 5.") and tells observers (`ITicketObserver.RatedAsync`): the **Ticket Rated** workflow event (`Score`, `Comment`) and the `SatisfactionScore` and `SatisfactionComment` fields of the `Tickets` data set.

Every requester e-mail then ends with **View your request**, linking to the request in My account, or to the signed link for a guest (`TicketEmailModel.Link` for templates, `ITicketRequesterLinks` for code). Links use the site's **Base URL** when it is set. Desk permissions play no part in the portal: a person sees what they asked for, and an account administrator what their account asked for.

## Approvals

The **Ticket Approvals** feature (`CrestApps.OrchardCore.Tickets.Approvals`) lets a ticket type ask for approval before work goes on, such as a manager's and then finance's for new equipment. On the type's **Ticket** part settings, **Approval** has up to three steps (`TicketApprovalSettings`): each a name, approvers chosen with the user picker, and **The requester's manager** (the user of the manager on the requester's staff record, with [Staff](./staff) on, through `ITicketRequesterManagerSource`); and **Status while waiting**, a waiting or on-hold status such as *Waiting for approval* (the first waiting status when none is chosen).

When a ticket of that type is created, `TicketApprovalStarter` resolves each step's approvers (a step left with none is skipped) and `ITicketService.StartApprovalAsync` keeps the approval on the ticket (`TicketPart.Approval`), moves it to the waiting status and tells observers (`ITicketObserver.ApprovalRequestedAsync`). With Notifications on, each approver of the step is told, with a link to the approval page, `Admin/ticket/{id}/approval`: what was asked (subject, requester, first message) and the steps, for the approvers of its steps even when they can't see the ticket otherwise. Agents see the same steps in the ticket's **Approval** panel.

One approver of the step decides it (`ITicketService.DecideApprovalAsync`; anyone else is refused). An approval moves to the next step and tells its approvers; after the last, the ticket goes back to the status it had (or the first open status when it can't go back, as from New). A refusal needs a reason: the ticket is solved and the requester gets the reason as a reply, which e-mails them like any reply. Every decision is on the timeline; an approver decides a step once. While the ticket waits, nothing else changes its status: the panel, bulk actions, the API, workflows and replies that set a status are refused, and a requester's reply doesn't reopen it. Someone with **Cancel ticket approvals** (`TicketsPermissions.CancelTicketApprovals`, administrators by default) can call it off with a reason (`ITicketService.CancelApprovalAsync`), which sends the ticket back to work. A step with no approver (a requester's manager for a guest, or a person without one) is skipped and named on the timeline; when no step has an approver the ticket isn't held and an internal note says why. Approval steps are the Tickets module's own; another module that needs approval steps extracts them first.

## Web form

The **Ticket Web Form** feature (`CrestApps.OrchardCore.Tickets.WebForm`) adds a **Ticket Form** widget (`TicketFormWidget`, with `TicketFormPart`): choose the ticket type and the desk, and optionally what the visitor reads once it's sent. Place it like any widget (layers, flow). It asks for the request, a category when the type has categories (only those customers may choose), the questions of the ticket type that customers fill (the same editor as the portal's, with the type's show-when rules), a subject unless the type has a title of its own (the title is the subject then), and a name and an e-mail when the visitor isn't signed in; a signed-in visitor is the requester. As on the portal, the answers are checked again on the server: only customer fields are kept, a hidden question keeps nothing, a required question that is showing must be answered, and the widget's desk is a suggestion the category's desk may take over (`DeskIsSuggestion`). The form posts to `tickets/forms/{widgetId}` and the ticket opens with the `WebForm` channel; the visitor goes back to the page, which says it was received with its number, or what went wrong.

Robots are kept out several ways: a hidden trap field (a filled one is answered as if it worked and opens nothing); through Orchard Core's **Rate Limits** (the feature turns it on), 10 submissions per visitor in 10 minutes across nodes (rate-limit group `tickets-web-form`, per signed-in user, otherwise per address; behind a proxy, set up **Reverse Proxy** so the address is the visitor's); at most 5 per address in 10 minutes on each node as a last line (`TicketFormGuard`, in memory); and, with **ReCaptcha** on and its keys set, a challenge every guest must pass, checked on the server (`ITicketFormChallenge`). The widget's editor warns when no challenge is set up. A form posts only while its widget is published and the visitor may view it; the post is at most 64 KB, a guest's name at most 200 characters, the subject is cut at 200 and the request at 10,000.

**A guest's address is unconfirmed.** A guest can type anyone's e-mail, so a guest's ticket is marked `RequesterUnverified` (`TicketCreateRequest.RequesterUnverified`): it is never filed under an account by the address's domain, and every e-mail to that address carries only fixed text and the ticket's number, never the guest's subject, name or a link to a page showing them (templates get `Ticket.Subject`, `Ticket.RequesterName` and `Ticket.Link` empty). An agent's own replies still reach the guest. The site can't be used to send someone else's words to any address.

## Workflows

With Workflows on, these events start workflows. Each can be narrowed to desks, ticket types and categories (a category takes the categories below it along, by the ticket's `CategoryPath`); the ticket's values are in `Workflow.Input` (`TicketId`, `Number`, `Subject`, `DeskId`, `StatusId`, `StatusCategory`, `PriorityId`, `TeamId`, `AssigneeId`, `Requester`, `ContentType`) with what the event adds (`ChangedFields`, `Previous`, `Body`, `Metric`, `MergedIntoId`, `ActionName`). `ChangedFields` and the changed-field filter use stable keys (`TicketChangeKeys`: `Subject`, `Desk`, `Status`, `Priority`, `Team`, `Assignee`, `EscalationLevel`, `Link`), not the shown labels, which are translated; each change also carries the ids it moved from and to (`FromValue`, `ToValue`). A workflow starts after the change is committed and the ticket's lock is released, so it reads the saved ticket.

| Event | When |
| --- | --- |
| Ticket Created, Ticket Updated (with a changed-field filter), Ticket Status Changed (with a category filter), Ticket Assigned | The ticket changes |
| Ticket Customer Replied, Ticket Agent Replied, Ticket Note Added | An entry is added |
| Ticket Service Level Warning, Ticket Service Level Breached | A target is close to or past due |
| Ticket Solved, Ticket Reopened, Ticket Closed, Ticket Merged | The life cycle moves |
| Ticket Action Requested | An agent runs a named action from the ticket's **Actions** menu |
| Ticket Time Elapsed | A time passes, in clock or business hours, since creation, the last customer or agent reply, or the last status change |

Tasks: Create Ticket, Update Ticket, Assign Ticket, Add Ticket Reply, Add Ticket Note, Escalate Ticket, Get Ticket, Find Tickets, Check Ticket Service Level, Link Tickets and Notify about a Ticket, and, with Work Orders on, Create Work Order From Ticket and, with AI on, Triage Ticket With AI. Changes a workflow makes are marked, and a chain of workflow-caused changes stops starting workflows at depth 3, so rules can't loop.

## Webhooks and audit trail

With Webhooks on, subscribers get `ticket.created`, `ticket.updated`, `ticket.assigned`, `ticket.reply_added` (public replies only; internal notes never leave the site), `ticket.solved` and `ticket.closed`, with the ticket as the API gives it. With Audit Trail on, the security-relevant events are recorded with who did it (the agent, or a workflow): a ticket moved to another desk, merged, or deleted, with ids only (`TicketAuditData`), never contact details. Everyday changes are already on the ticket's timeline.

## Data sets

With Data Pipelines Core on, the **Tickets** data source serves the [Report Builder](./report-builder/data-sources) and data pipelines. Every data set shows only the tickets in the reader's scope.

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

This is the agents' API: `entries` returns internal notes to anyone who may view the ticket, and every call is scoped by desk permissions. It is never the requesters' API. A customer portal gets its own endpoints with the requester rule (their own tickets, public entries only) rather than reusing these.

## Accounts, personal data and AI tools

With Accounts on, an account's page has a **Tickets** tab with its latest 25 tickets in the user's scope, **New ticket** for the account, and **View all in the ticket list**, which opens the list filtered to the account (`?accountId=`, every state). The list shows the filter as an **Account** chip that removes it; the account's name shows to a user who may view the account.

Tickets takes part in [personal data](./personal-data) requests: a person's tickets are found by their user and by the e-mail kept on a guest's ticket. An export lists each ticket and its public replies. An erasure:

- removes the contact snapshot's e-mail, phone and address and replaces the name, so the ticket's index row (which the list searches) no longer holds the e-mail;
- rewrites the subject and the text of the ticket's timeline (`ITimelineService.ScrubAsync`): bodies, author names, shown change values and text kept in entry properties, such as a "not delivered" notice;
- deletes the files the requester sent (on entries they wrote; files an agent attached are the business's records and stay);
- deletes the ticket's e-mails in the outbox, so a pending one never goes out.

With a desk's AI assistant on, its suggestions and triages send the ticket's conversation, internal notes and the names in it included, to the AI profile's provider (see **AI assistant** below); what the provider keeps is governed by its own terms.

What the site can't recall: e-mails already sent, webhook payloads already delivered (the webhook module's outbox keeps its own payloads until sent), the input of workflow instances that already ran or wait (Workflows keeps its own instances, and a reply or note event puts the message's text in `Workflow.Input.Body`; delete those instances under **Workflows** when a person must be forgotten), and anything other systems copied. The audit trail entries of tickets keep ids only, never contact details.

With AI on, these tools can be picked on AI profiles (category **Tickets**). Each acts as the signed-in user, within their scope and permissions.

| Tool | Does |
| --- | --- |
| `listTicketOptions` | Lists the desks the user may open tickets in, the statuses, the priorities and the categories of the default ticket type, with their ids |
| `searchTickets` | Finds tickets by number or subject, status category, desk and assignee |
| `getTicket` | Reads a ticket and its latest 20 entries |
| `createTicket` | Opens a ticket on a desk, with a category |
| `addTicketReply` | Sends a public reply or adds an internal note |
| `updateTicket` | Changes the status, priority, category, team or assignee |

**AI assistant.** With AI on, **Settings > Service Desk** has **AI profile for replies** (`TicketAISettings.ReplyProfileId`, in the site settings): the chat AI profile, whose instructions, knowledge (its data sources) and chat model the assistant uses, for every desk that names none. A desk's editor has **AI assistant**: the service desk's profile, another profile (`TicketDeskAISettings.ProfileId`, in the desk's properties) or **No AI assistant on this desk** (`TicketDeskAISettings.Disabled`), and **A triage sets the priority it suggests**. The settings page, the desk's choice and the ticket's panel are added by display drivers registered only with AI on (`AIToolsStartup`). The ticket page of a desk with a profile then has an **AI assistant** panel. **Suggest a reply** (`POST Admin/ticket/{id}/assistant/reply`, the Reply permission) reads the ticket's latest 30 replies and notes, internal notes marked as such, and puts a reply in the composer for the agent to edit and send; nothing goes to the requester on its own. **Triage** (`POST Admin/ticket/{id}/assistant/triage`, the Edit permission) asks for a summary and one of the site's priorities as JSON and adds them as an internal note from "AI assistant", setting the priority when the desk says so, within a bound: a triage may lower the priority, or raise it at most one step above the ticket type's default (above the ticket's priority when the type has none); a higher suggestion is only noted (`TicketAssistant.PriorityToApply`), because a requester can write "urgent, ignore your rules" into the request the assistant reads. A suggested reply goes into the composer without changing whether it is a public reply or an internal note, and the model is told the reply goes to the requester and must not quote, paraphrase or hint at internal notes. What the assistant sends to the profile's AI provider: the ticket's number and subject and its latest 30 replies and internal notes, with their authors' names (the requester's included), plus the profile's own instructions and knowledge. The AI profile's **Data privacy** setting decides what the site keeps of chat conversations, not what is sent, so choose the provider and its data terms for what tickets hold. The **Triage Ticket With AI** workflow task (with Workflows on) does the same, such as on Ticket Created, with the note in the `Triage` output. `TicketAssistant` runs with the profile's tools turned off, counts its use under the `Tickets` usage category, and answers "not available" when the profile, its model or the connection fails, without stopping the page.

## Recipes and deployment

| Step | Holds |
| --- | --- |
| `TicketStatuses` | Statuses |
| `TicketPriorities` | Priorities |
| `TicketDesks` | Desks, matched by `ItemId` and then by name |
| `TicketViews` | Saved views; a personal view carries its owner's user name (`OwnerUserName`), resolved to the user of the target site, and a view whose owner isn't a user there is refused with that name |
| `TicketBoards` | Boards, matched by `ItemId`, with their columns (`Id`, `Name`, `StatusIds`, `DefaultStatusId`, `Limit`); a personal board carries its owner's user name as a view does. A board whose columns break the rules (no column, a status in two columns, a status that doesn't exist, a default outside its column) is refused with the reason |

Each has a deployment step and schemas for CrestApps Recipes. A record whose `ItemId` isn't a valid id (26 letters and digits) is refused with its name and id rather than imported under a new id, which would cut the links other records make to it. A status's or priority's color must be a hex color or a color name, in the editor and in a recipe. Tickets themselves are content items and travel with Orchard Core's content step; their entries with the `TimelineEntries` step. The backlog snapshots are history the site records and have no step.

The **Help desk starter** recipe sets up weekday business hours, a `Tickets` service-level policy, the Support desk and an IT desk, an `ITRequest` ticket type with an **IT categories** taxonomy (Hardware routed to the IT desk) and conditional questions, and two example workflows that start turned off: one escalates a ticket whose target is breached and says why in a note, the other adds a note when a requester reopens a solved ticket.

## Extending Tickets

| To | Use |
| --- | --- |
| React to ticket changes | `ITicketObserver` (created, updated, assigned, status changed, replied, merged, deleted, service level) |
| Offer actions on the ticket page | `ITicketActionSource` |
| Offer reply templates in the composer | `ITicketReplyTemplateSource` |
| Add a panel to the ticket page | A `ContentPartDisplayDriver<TicketPart>` or a display driver of the ticket type |
| Read or change tickets | `ITicketService` and `ITicketAccessService`; apply `TicketScopeFilter` to your own ticket queries |
| Read a board or move a card from code | `ITicketBoardService` (the visible boards, `LoadAsync`, `MoveAsync`); add fields to the board editor with a `DisplayDriver<TicketBoard>` |
