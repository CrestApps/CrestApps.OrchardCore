---
sidebar_label: Overview
title: Service Desk
description: "Answer requests from customers and employees as tickets: who does what, the Service Desk menu and settings, the features to turn on, and where to find each task."
technical_manual:
  - modules/tickets
---

The **Service Desk** is where your team answers requests. Each request is a **ticket**: a customer who can't sign in, an employee who needs a laptop. Each ticket belongs to a **desk**, such as Support, IT or HR, which decides who may see it. Agents reply to the requester, write internal notes for each other, and move the ticket through its statuses until it is solved. Customers send requests from their account on your site or a form on a page, and find answers on their own in the help center.

| | |
| --- | --- |
| **Menu** | **Service Desk**: Tickets, Boards and the boards you pinned, Knowledge Base (Articles and Help center), and for those who manage the service desk, Desks, Statuses, Priorities and Views; **Settings > Service Desk** for the notification templates and the AI profile for replies |
| **Permission** | A view permission on a desk, such as **View the tickets of my teams in Support**; **Manage ticket settings** for the desks, statuses, priorities, views and settings; **Manage shared ticket boards** or **Manage my own ticket boards** to make boards; **View ticket reports** for the reports; **List content items** for the Knowledge Base menu, and each article's **Which roles can see this content** for who reads it |
| **Feature** | Tickets |

<AskYourAdmin />

## Who does what

| Person | What they do | Start with |
| --- | --- | --- |
| **Agent** | Works the ticket list and boards, answers requesters, writes internal notes, records time | [Work the ticket list](./ticket-list.md), [Answer a ticket](./answer-a-ticket.md) |
| **Desk manager** | Shapes desks, statuses, priorities, views and boards, ticket types and categories, and service levels; reads the reports | [Desks](./desks.md), [Reports](./reports.md) |
| **Administrator** | Turns on the features, gives roles their desk permissions, sets up notification templates, the AI assistant and the portal | [Features to turn on](#features-to-turn-on), [Notification templates](./notification-templates.md) |
| **Requester** | Sends a request, follows it and replies from their account or a link in their e-mails, rates a solved request, and reads the help center | [Tickets Portal](./portal.md), [Knowledge Base](./knowledge-base.md) |

## The Service Desk menu

- **Tickets**: the ticket list and its saved views.
- **Boards**: tickets as cards in columns, and under it each board you pinned to your menu.
- **Knowledge Base**: **Articles**, the help articles with their **New Knowledge Article** button, and **Help center**, the public page at `/help`.
- **Desks**, **Statuses**, **Priorities** and **Views**: the service desk's setup, for those who may manage ticket settings.

**Settings > Service Desk** holds the **Notification templates**, the messages requesters get, and, with AI on, the **AI profile for replies**.

The permissions to view and work tickets exist for each desk, so HR tickets can stay with HR. Give a role **View all tickets**, **View the tickets of my teams** or **View the tickets assigned to me** on a desk to decide what its users see there.

## Features to turn on

Turn them on under **Tools > Features**:

| Feature | What it adds |
| --- | --- |
| **Tickets** | The ticket list, tickets, desks, statuses, priorities, views and boards, service levels and notifications |
| **Tickets Portal** | **Requests** under **My account**, where people send and follow their requests |
| **Ticket Web Form** | A **Ticket Form** widget for any page |
| **Ticket Approvals** | Approval steps before work starts on a ticket type |
| **Tickets for Contact Center** | Offers a desk's new tickets to a Contact Center queue's agents |
| **Knowledge Base** | Help articles, categories and the help center at `/help` |
| **Tickets API** | Lets other systems read and change tickets |

To start with a working example, run the [help desk starter recipe](./help-desk-starter.md).

## Where to go next

**Working tickets**

- [Work the ticket list](./ticket-list.md)
- [Open a ticket for someone](./open-a-ticket.md)
- [Answer a ticket](./answer-a-ticket.md)
- [Protected information](./protected-information.md)
- [Merge and link tickets](./merge-and-link.md)
- [Boards](./boards.md)
- [Time on tickets](./time-on-tickets.md)
- [Send someone on site](./work-orders.md)
- [Contact Center routing](./contact-center-routing.md)
- [Notifications](./notifications.md)

**Setting up the service desk**

- [Desks](./desks.md)
- [Statuses and priorities](./statuses-and-priorities.md)
- [Saved views](./views.md)
- [Ticket types, fields and categories](./ticket-types-and-categories.md)
- [Service levels](../administration/service-levels.md)
- [Notification templates](./notification-templates.md)
- [AI assistant](./ai-assistant.md)
- [Approvals](./approvals.md)
- [Help desk starter recipe](./help-desk-starter.md)

**For customers**

- [Tickets Portal](./portal.md)
- [Request form on a page](./web-form.md)
- [Knowledge Base](./knowledge-base.md)

**Results**

- [Reports](./reports.md)

For an end-to-end guide, see [Run a help desk](../use-cases/help-desk.md).
