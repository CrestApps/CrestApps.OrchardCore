---
sidebar_label: Ticket List
title: Work the Ticket List
description: "Find tickets with saved views, search and filters, act on several at once, and see an account's tickets and service level."
technical_manual:
  - modules/tickets
  - modules/tickets/permissions
---

The ticket list shows the tickets you may see, latest change first. Which tickets those are depends on the desks you have a view permission on: all of a desk's tickets, your teams' tickets, or only the ones assigned to you.

| | |
| --- | --- |
| **Menu** | Service Desk > Tickets |
| **Permission** | A view permission on a desk, such as **View the tickets of my teams in Support** |
| **Feature** | Tickets |

<AskYourAdmin />

## Find tickets

Go to **Service Desk > Tickets**.

- Pick a **saved view** above the list, such as **My open tickets** or **Unassigned**. See [Saved views](./views.md) to make your own.
- Search by ticket number or words of the subject.
- Narrow the list by state, status, priority, assignee, team, desk, ticket type, category (a category takes the ones under it along) or service level (**Past a target**, **Due within 4 hours**), and sort it.
- Switch between the list and the grid layout with the buttons above the list.

Each row shows the ticket's number and subject, its status, priority and desk, the requester and assignee, and when it is due. A **Protected** badge marks a ticket that holds [protected information](./protected-information.md).

## Act on several tickets at once

Select tickets with their boxes, then choose an action:

- **Assign to me**
- set a status
- **Merge** them into the oldest one
- delete them

Each ticket is checked on its own: one you may not change is left as it was, with a message.

## An account's tickets

An account's page has a **Tickets** tab: its latest tickets you may see, how many are open, **New ticket** for the account, and **View all in the ticket list**, which opens the ticket list showing only that account's tickets, in every state. Remove the **Account** filter there to see every ticket again.

The account's **Service** tab starts with **Service level**: the service level its tickets get. A ticket covered by one of the account's [agreements](../administration/agreements.md) gets that agreement's policy; the account's other tickets get the account's own policy, or, without one, their category's, ticket type's or desk's. Select **Change** to set the account's own policy.
