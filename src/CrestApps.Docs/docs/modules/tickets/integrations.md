---
sidebar_label: Integrations
title: Integrations
description: "How Tickets works with other modules: the account tab and service level card, agreements, work orders, time on tickets, tickets from messaging conversations, routing through Contact Center queues, and personal data requests."
user_manual:
  - user-manual/service-desk/work-orders
  - user-manual/service-desk/time-on-tickets
  - user-manual/service-desk/contact-center-routing
  - user-manual/service-desk/open-a-ticket
---

## Accounts

With Accounts on, an account's **Service** tab has a **Service level** card (`AccountServiceLevelDisplayDriver`, for users who may view tickets): the policy of each agreement in force that names one, for the tickets it covers, then the account's own (`TicketAccountPart`), with **Change** to edit the account and links to the policies for those who manage them. An account's page also has a **Tickets** tab with its latest 25 tickets in the user's scope, **New ticket** for the account, and **View all in the ticket list**, which opens the list filtered to the account (`?accountId=`, every state). The list shows the filter as an **Account** chip that removes it; the account's name shows to a user who may view the account.

How an account's policy and e-mail domains apply to new tickets is described in [Service levels](./service-levels#accounts).

## Agreements

With [Agreements](../agreements) on, a ticket works under an agreement with its account (`TicketPart.AgreementId`, in the index). An agreement covers tickets when it has `TicketAgreementTerms` (its **Tickets** panel): the desks and ticket types it covers (none for all) and the service-level policy of its tickets. A new ticket with an account takes the earliest agreement in force whose terms cover its desk and type (`ITicketAgreementResolver`), unless the request names one of the account's agreements (`TicketCreateRequest.AgreementId`); the agreement's policy comes before the account's entitlement, the category's, the type's and the desk's. Agents change it under **Planning** (`TicketUpdate.AgreementId`, empty to clear), with change key `Agreement` on the timeline; another account's agreement is refused, and a new agreement's policy retargets the ticket's timers. With Time Tracker on, the time logged on the agreement's tickets counts against its hours (`TicketAgreementUsageProvider`): the time in a desk's ticket stage, not in its stage for time not billed, on tickets whose billing type isn't not billable or no charge. Each time entry is charged to the agreement its ticket works under when the time is logged (`TicketTimeChargeResolver`, Time Tracker's `TimeEntry.ChargedTo`), so changing a ticket's agreement moves only the time logged after the change; the time before stays on the old agreement's hours, balance and reports. Time logged before entries remembered their agreement was charged, on upgrade, to the agreement its ticket had then. The ticket's page shows its agreement with the hours left to those who may view agreements.

## Work orders

With Work Orders on, the ticket page has a **Work order** panel for agents who may change the ticket and hold **Manage work orders**: they choose a work order type and, optionally, a priority, and `TicketWorkOrders` creates the work order through `WorkOrderService` (the type's defaults, number and service level apply). The work order takes the ticket's subject as its summary (shortened to 120 characters), the first message as its description, the ticket's account, the requester (the contact when the requester is one, with name, e-mail, phone and address) and `Source` `Ticket` with the ticket's id as `SourceId`; the ticket links to it (**Related**), so it shows under **Links**. When the work order is completed or canceled, `TicketWorkOrderObserver` adds an internal event to the ticket's timeline (with the cancel reason); the ticket's status is left to the agent. The **Create Work Order From Ticket** workflow task does the same from a workflow, with the type by name or id, and sets the `WorkOrderId` and `WorkOrderNumber` outputs. This is an integration of the Tickets feature (`[RequireFeatures]`), not a feature of its own.

## Time on tickets

With Time Tracker on, a desk's editor has **Time on tickets**: the Time Tracker stage its agents' ticket work is recorded in (`TicketDeskTimeSettings.StageId`, in the desk's properties). The ticket page then has a **Time** panel. **Start working on this ticket** switches the agent's time to that stage for the ticket (`ITimeTrackerService.SwitchAsync` with `Source` `Ticket` and `SourceRef` `Ticket:{id}`), so time on tickets is part of the person's day, with no second ledger; starting another ticket ends the first entry. **Stop working on this ticket** goes back to the stage the agent was in before they started on tickets, or clocks them out if they started on the ticket. The panel adds up each person's time on the ticket from the entries for it (`TicketTime.SummarizeAsync`, open entries up to now); people whose time another clock keeps can't start from a ticket, as with any switch. Time entries are corrected in Time Tracker. This is an integration of the Tickets feature (`[RequireFeatures]`), not a feature of its own.

**Log time** on the panel records time spent after the fact (`TicketTime.LogAsync` with a `TicketTimeLog`): the time spent as people write it (`30`, `45m`, `1h 30m`, `1:30`, `1.5h`, read by `TicketTimeDuration.ParseMinutes`, up to 24 hours), when it started in the agent's time zone (empty when the work just ended), a note and whether it is billable. The entry goes through `ITimeTrackerService.InsertAsync`, so it takes its place in the agent's day: the time it covers, such as their Work, is cut around it and their total doesn't change. Another ticket's time and time another clock keeps are never cut; logging over them is refused. A desk may also name **Time not billed to the customer** (`TicketDeskTimeSettings.NotBilledStageId`), a second stage for time logged as not billable; give each stage the time categories it counts toward (Billable only on the first), so Time Tracker's own reports agree with the ticket reports. The panel lists the ticket's entries with their notes and what wasn't billed, and links users with **Edit anyone's time** to the time entries page, where entries are corrected or removed.

## From a messaging conversation

With the [messaging workspace](../../omnichannel/messaging-workspace) on, each conversation row has **Open a ticket**. It opens **New ticket** with the customer as requester (their contact, or their address as a name), a subject and the latest 30 messages, oldest first, with who wrote each, as the first message, for an agent who may read the conversation (`TicketConversationSource`). The new ticket links back to the conversation (`MessagingConversation:{id}`, Related).

## Contact Center routing

The **Tickets for Contact Center** module (`CrestApps.OrchardCore.Tickets.ContactCenter`) lets a desk offer its tickets through a Contact Center queue, so the queue's presence, capacity and skills routing decide who gets them. A desk's editor gets **Contact Center queue** (`TicketDeskQueueSettings`, in the desk's properties). A new ticket on that desk that nobody has (and that isn't waiting for approval) becomes a task activity (`ActivityKind.Task`, source `Api`, the ticket's number and subject as its instructions, the ticket's id in `TicketActivityLink`) enqueued with the ticket's priority (`IActivityQueueService.EnqueueAsync`); the ticket links to the activity (`OmnichannelActivity:{id}`), which shows on its timeline. When an agent accepts the offer (`QueueItemAssigned`), `TicketOfferAcceptedHandler` gives them the ticket, unless someone already has it; replaying the event changes nothing. When the ticket is given to someone by hand, solved, closed, merged or deleted before an agent took it, the queued work is withdrawn (`IQueuedWorkWithdrawalService`); work an agent already took stays theirs to complete in the agent workspace, like any activity. Agents accept offers in the agent desktop, which needs the Contact Center Voice and real-time features.

## Personal data

Tickets takes part in [personal data](../personal-data) requests: a person's tickets are found by their user and by the e-mail kept on a guest's ticket. An export lists each ticket and its public replies. An erasure:

- removes the contact snapshot's e-mail, phone and address and replaces the name, so the ticket's index row (which the list searches) no longer holds the e-mail;
- rewrites the subject and the text of the ticket's timeline (`ITimelineService.ScrubAsync`): bodies, author names, shown change values and text kept in entry properties, such as a "not delivered" notice;
- deletes the files the requester sent (on entries they wrote; files an agent attached are the business's records and stay);
- deletes the ticket's e-mails in the outbox, so a pending one never goes out.

A desk's AI assistant sends the ticket's conversation to the AI profile's provider (see [AI tools and assistant](./ai)).

What the site can't recall: e-mails already sent, webhook payloads already delivered (the webhook module's outbox keeps its own payloads until sent), the input of workflow instances that already ran or wait (Workflows keeps its own instances, and a reply or note event puts the message's text in `Workflow.Input.Body`; delete those instances under **Workflows** when a person must be forgotten), and anything other systems copied. The audit trail entries of tickets keep ids only, never contact details.
