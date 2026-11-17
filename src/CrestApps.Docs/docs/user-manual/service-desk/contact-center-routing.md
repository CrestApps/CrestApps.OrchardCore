---
sidebar_label: Contact Center Routing
title: Route Tickets Through the Contact Center
description: "Offer a desk's new tickets to a Contact Center queue's agents, by their skills and how busy they are, and give the ticket to the agent who accepts."
technical_manual:
  - modules/tickets/integrations
---

A desk can hand its new tickets to a Contact Center queue, so the queue's routing (who is available, their skills and how busy they are) decides who gets each one.

| | |
| --- | --- |
| **Menu** | Service Desk > Desks, then a desk |
| **Permission** | Manage ticket settings |
| **Feature** | Tickets for Contact Center |

<AskYourAdmin />

## Set it up

1. Turn on **Tickets for Contact Center** under **Tools > Features**.
2. Go to **Service Desk > Desks** and edit a desk.
3. Choose its **Contact Center queue** and select **Save**.

## What happens

- New tickets on that desk that nobody has yet (and that aren't waiting for approval) are offered to the queue's available agents, by their skills and how busy they are.
- The agent who accepts one gets the ticket. Agents accept offers in the agent desktop. See [Agent workspace](../agent-workspace.md).
- If someone takes the ticket by hand, or it's solved, closed, merged or deleted first, the offer is called off. Work an agent already took stays theirs to complete.

See [Queues](../queues.md) and [Skills and agent entitlements](../skills-and-entitlements.md) to shape who the queue offers work to.
