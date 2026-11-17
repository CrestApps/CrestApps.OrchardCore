---
sidebar_label: Help Desk Starter Recipe
title: Start From the Help Desk Recipe
description: "Run the Help desk starter recipe for working business hours, a service level, two desks, an IT request type with categories and questions, and example workflows."
technical_manual:
  - modules/tickets
---

The **Help desk starter** recipe sets up a working service desk you can try and then change to fit.

| | |
| --- | --- |
| **Menu** | Configuration > Recipes |
| **Permission** | Manage recipes |
| **Feature** | Tickets |

<AskYourAdmin />

## Run it

1. Turn on **Tickets** under **Tools > Features**.
2. Go to **Configuration > Recipes**, find **Help desk starter** and select **Run**.

## What it sets up

- Weekday business hours.
- A standard service level for tickets.
- The **Support** desk and an **IT** desk.
- An **IT request** ticket type with **IT categories** (Hardware is routed to the IT desk) and questions that follow them: the laptop model for laptop problems, which system for access requests, and who else is affected for an outage. Its **Root cause** field, for agents only, is a multi-line box.
- Two example workflows that start turned off: one escalates a ticket that misses a target and says why in a note, the other adds a note when a requester reopens a solved ticket. Turn them on under **Workflows** when you want them.

Everything it adds is yours to rename and change: see [Desks](./desks.md), [Ticket types, fields and categories](./ticket-types-and-categories.md) and [Service levels](../administration/service-levels.md). See [Recipes](../administration/recipes.md) for running recipes in general.
