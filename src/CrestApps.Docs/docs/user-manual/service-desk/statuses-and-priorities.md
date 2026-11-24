---
sidebar_label: Statuses and Priorities
title: Statuses and Priorities
description: "Name the statuses tickets move through, each in a category, with the label requesters see, a color and whether it pauses service-level targets; set the priorities and the priority matrix."
technical_manual:
  - modules/tickets/desks-and-settings
---

| | |
| --- | --- |
| **Menu** | **Service Desk** → **Statuses**, **Service Desk** → **Priorities** |
| **Permission** | Manage ticket settings |
| **Feature** | Tickets |

<AskYourAdmin />

## Statuses

A status is where a ticket stands, such as **Open** or **Waiting for the requester**. Go to **Service Desk** → **Statuses** and select **Add ticket status**, or a status to edit it.

| Field | What it does |
| --- | --- |
| **Name** | What agents see |
| **Category** | **New** (nobody has worked on it yet), **Open** (an agent is working on it), **Pending** (waiting for the requester), **On hold** (waiting for someone else), **Solved** (answered, reopens on a reply) or **Closed** (finished and read-only). The category is what the life cycle, service levels and reports go by, whatever the status is called |
| **What customers see** | The label requesters see in their account. Leave empty to show the name |
| **Pauses service-level timers** | While a ticket is in this status, its first response and resolution targets stop counting |
| **Color** | A color or a color name for the badge |
| **Order** | Lower comes first; the first status of the New category is the one new tickets start in |

## Priorities

Go to **Service Desk** → **Priorities** and select **Add ticket priority**, or a priority to edit it.

| Field | What it does |
| --- | --- |
| **Name** | What people pick. Service-level targets are set per priority name |
| **Rank** | Higher is more urgent; lists sorted by priority put the highest first |
| **Color** | A color or a color name for the badge |

## The priority matrix

Agents set a ticket's **Impact** (how much of the business it touches) and **Urgency** (how soon it must be dealt with) under **Planning** on the ticket. A desk's **Priority matrix** gives the priority for each pair: edit the desk and choose a priority in each cell. When an agent sets a ticket's impact and urgency, it gets the priority of their cell, unless they choose a priority themselves; a new ticket with both gets it too. A cell left empty leaves the priority alone. See [Desks](./desks.md#priority-matrix).
