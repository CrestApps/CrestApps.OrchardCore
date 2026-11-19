---
sidebar_label: Desks
title: Desks
description: "Set up each desk: who it serves, its ticket types, how new tickets are assigned, its business hours and service level, what happens to solved tickets, who is told, and its priority matrix, time and AI choices."
technical_manual:
  - modules/tickets/desks-and-settings
  - modules/tickets/assignment-and-life-cycle
---

A **desk** keeps its own tickets, such as Support, IT or HR. Who may see and work them is decided by desk permissions, so each team keeps its tickets to itself.

| | |
| --- | --- |
| **Menu** | Service Desk > Desks |
| **Permission** | Manage ticket settings |
| **Feature** | Tickets |

<AskYourAdmin />

## Add or edit a desk

Go to **Service Desk > Desks** and select **Add desk**, or a desk's name to edit it. The editor groups its fields in cards.

### Desk

| Field | What it does |
| --- | --- |
| **Name**, **Description** | How people pick the desk |
| **Serves** | **Customers** or **People inside the organization**. Customers can only send requests on desks that serve customers; with Staff on, members of staff also see the desks that serve people inside the organization |
| **Ticket types** | The kinds of tickets the desk takes. Leave none selected to offer every ticket type |

### Routing

| Field | What it does |
| --- | --- |
| **Default team** | New tickets go to this team when their type names none, and are assigned to its members |
| **New tickets go to** | **Nobody: an agent takes them**, **The team member with the fewest open tickets**, or **Each team member in turn** |
| **A team member can take tickets now when** | The checks that say a member can take work now: they're on a published shift, they aren't out of office in the calendar, and they're signed in to the Contact Center. Leave a check off when the people of this desk don't use it, such as the Contact Center phone for people who only work tickets |

Only members who can take work now, and who hold the skills the ticket type asks for, are chosen. When no one can, the ticket stays unassigned and its history says why.

### Business hours and service level

| Field | What it does |
| --- | --- |
| **Business hours** | The hours the desk counts in, such as for workflows that wait a number of business hours |
| **Service-level policy** | Used when the ticket's type names no policy. Policies are written under **Content > Service Levels**, for tickets. See [Service levels](../administration/service-levels.md) |

### Solved tickets

| Field | What it does |
| --- | --- |
| **Reopen window (days)** | A reply from the requester inside this many days after solving reopens the ticket; a later one opens a follow-up |
| **Close solved tickets after (days)** | Solved tickets close by themselves this many days after they were solved. 0 keeps solved tickets until someone closes them |
| **Delete protected information after (days)** | Days after a ticket is closed that its [protected information](./protected-information.md) is deleted. 0 keeps it |

### Requesters and assignees

| Field | What it does |
| --- | --- |
| **Tell the requester** | Requesters hear when their ticket is received, answered and solved: through their notifications when they have an account on the site, otherwise by e-mail. The messages are written under **Settings > Service Desk**. See [Notification templates](./notification-templates.md) |
| **Tell the assignee** | Agents hear when a ticket is assigned to them, and the members of its team when it is given to their team |
| **Ask requesters to rate a solved ticket after (hours)** | With Tickets Portal on and requesters told, the requester gets one message with a link to rate how their request was handled. 0 never asks |

### Priority matrix

The priority a ticket gets for each impact and urgency. When an agent sets a ticket's impact and urgency, it gets the priority of their cell, unless they choose a priority themselves. A new ticket with both gets it too. Leave a cell empty to leave the priority alone. See [Statuses and priorities](./statuses-and-priorities.md#the-priority-matrix).

### Time on tickets

With Time Tracker on, the **Time tracking** card has:

| Field | What it does |
| --- | --- |
| **Time on tickets** | The Time Tracker stage agents' work on this desk's tickets is recorded in. Agents start and stop working on a ticket from its page, or log time they spent, and the ticket shows the time each person spent on it |
| **Time not billed to the customer** | A second stage, for time agents log as not billable. Give each stage the time categories it counts toward (Billable only on the first one), so the time reports agree |

See [Time on tickets](./time-on-tickets.md).

### AI assistant

With AI on, choose the **AI profile** the desk's tickets use (the service desk's profile, another one, or **No AI assistant on this desk**) and whether **A triage sets the priority it suggests**. See [AI assistant](./ai-assistant.md).

### Contact Center queue

With Tickets for Contact Center on, choose the queue the desk's new tickets are offered to. See [Contact Center routing](./contact-center-routing.md).

Select **Save** when you're done.

## Who sees a desk's tickets

The permissions to view and work tickets exist for each desk, under **Users > Roles**:

| Permission | Lets a user |
| --- | --- |
| **View all tickets in** a desk | See every ticket of the desk |
| **View the tickets of my teams in** a desk | See the tickets of their teams and the ones assigned to them |
| **View the tickets assigned to me in** a desk | See only the tickets assigned to them |
| **Edit tickets**, **Reply to tickets**, **Add internal notes to tickets**, **Assign tickets**, **Merge tickets**, **Delete tickets** in a desk | Do that on the tickets they may see |
| **View protected information in** a desk | Show and remove [protected information](./protected-information.md) |

Each also exists once over all desks. See [Roles](../administration/roles.md).
