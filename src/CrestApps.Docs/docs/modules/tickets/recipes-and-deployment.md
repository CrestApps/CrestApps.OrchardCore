---
sidebar_label: Recipes and deployment
title: Recipes and deployment
description: "The recipe and deployment steps of the service desk's settings records, the rules they check, and the Help desk starter recipe."
user_manual: user-manual/service-desk/help-desk-starter
---

| Step | Holds |
| --- | --- |
| `TicketStatuses` | Statuses |
| `TicketPriorities` | Priorities |
| `TicketDesks` | Desks, matched by `ItemId` and then by name |
| `TicketViews` | Saved views; a personal view carries its owner's user name (`OwnerUserName`), resolved to the user of the target site, and a view whose owner isn't a user there is refused with that name |
| `TicketBoards` | Boards, matched by `ItemId`, with their columns (`Id`, `Name`, `StatusIds`, `DefaultStatusId`, `Limit`); a personal board carries its owner's user name as a view does. A board whose columns break the rules (no column, a status in two columns, a status that doesn't exist, a default outside its column) is refused with the reason |

Each has a deployment step and schemas for CrestApps Recipes. A record whose `ItemId` isn't a valid id (26 letters and digits) is refused with its name and id rather than imported under a new id, which would cut the links other records make to it. A status's or priority's color must be a hex color or a color name, in the editor and in a recipe. Tickets themselves are content items and travel with Orchard Core's content step; their entries with the `TimelineEntries` step. The backlog snapshots are history the site records and have no step.

## The Help desk starter recipe

The **Help desk starter** recipe sets up weekday business hours, a `Tickets` service-level policy, the Support desk and an IT desk, an `ITRequest` ticket type with an **IT categories** taxonomy (Hardware routed to the IT desk) and conditional questions, and two example workflows that start turned off: one escalates a ticket whose target is breached and says why in a note, the other adds a note when a requester reopens a solved ticket.
