---
sidebar_label: Desks and settings
title: Desks and settings records
description: "The service desk's settings records: desks and what each one decides, statuses and their categories, priorities and the priority matrix, saved views, and the site settings under Settings → Service Desk."
user_manual:
  - user-manual/service-desk/desks
  - user-manual/service-desk/statuses-and-priorities
  - user-manual/service-desk/views
  - user-manual/service-desk/notification-templates
---

The service desk is set up with records under the **Service Desk** menu (**Desks**, **Statuses**, **Priorities** and **Views**), managed with `ManageTicketSettings`, and with site settings under **Settings** → **Service Desk**. Desks, statuses, priorities, saved views and boards are catalog records (`TicketDesk`, `TicketStatus`, `TicketPriority`, `TicketView`, `TicketBoard`), each read and written through its `ICatalog<T>` and `ICatalogManager<T>`; where they are kept is described in [Storage](./storage), and how they travel between sites in [Recipes and deployment](./recipes-and-deployment).

## Desks

A desk keeps its own tickets, with its own permissions (see [Permissions](./permissions)). A desk also has its business hours schedule (used by the **Time elapsed** workflow event counted in business hours; service-level targets use their policy's schedule), service-level policy, default team, ticket types, reopen window, auto-close delay, how new tickets are assigned and whether requesters are e-mailed and assignees told.

How a desk assigns new tickets and which availability checks it honors are described in [Assignment and life cycle](./assignment-and-life-cycle). Other modules add their own settings to a desk's editor: the AI assistant ([AI tools and assistant](./ai)), **Time on tickets** and the **Contact Center queue** ([Integrations](./integrations)), the rating delay ([Portal and web form](./portal-and-web-form)) and **Delete protected information after (days)** ([Protected information](./protected-information)).

## Statuses

Every status belongs to a category, which decides what it means for the ticket:

| Status category | Meaning |
| --- | --- |
| `New`, `Open` | Being worked on |
| `Pending` | Waiting for the requester; a status can pause the service-level timers |
| `OnHold` | Waiting for something else |
| `Solved` | Answered; a reply from the requester within the desk's reopen window reopens it |
| `Closed` | Final and read only; a reply after it opens a follow-up ticket |

## Priorities and the priority matrix

Priorities are kept in their order. A desk's **Priority matrix** (`TicketDesk.PriorityMatrix`, in the desk recipe step) gives the priority of each impact and urgency: when an update changes either and names no priority, or a new ticket has both and its request names no priority, the ticket gets the priority of its cell; a cell left empty leaves the priority alone. Impact and urgency are the ticket's planning values (see [Assignment and life cycle](./assignment-and-life-cycle#planning-values)).

## Saved views

A saved view (`TicketView`) keeps a `TicketQuery` of the ticket list. Shared views are managed under **Service Desk** → **Views**; a personal view belongs to its owner.

## **Settings** → **Service Desk**

**Settings** → **Service Desk** holds the site settings of the service desk: the **Notification templates** of the requester messages (see [E-mails and notifications](./emails-and-notifications)) and, with AI on, the **AI profile for replies** (see [AI tools and assistant](./ai)).
