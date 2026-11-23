---
sidebar_label: Boards
title: Boards
description: "Ticket boards: columns of statuses, moving a card to change a ticket's status, who a board is shared with and what it shows, live updates, pins and the board permissions."
user_manual: user-manual/service-desk/boards
---

A board (`TicketBoard`, **Service Desk** → **Boards**, `Admin/tickets/boards`) shows tickets as cards in columns. Each column (`TicketBoardColumn`) holds one or more statuses, such as a **Pending** column for **Waiting for the requester** and **Waiting for a third party**; a status is in one column of a board only, and tickets in a status that is in no column don't show on that board. Dragging a card to another column (or choosing **Move** on the card, for the keyboard and touch screens) sets the ticket's status: to the column's only status, to its **default** when it has several, or to the one the agent picks when it has several and no default. A column can have a limit; the header turns red when the column holds more, and moves are never refused for it.

## What a board shows

A board keeps a `TicketQuery` for which tickets it shows (desks, teams, ticket types, assignee and the order of the cards; the columns decide the statuses) and is shared with everybody, with a team, or kept by its owner. Whoever opens it sees only the tickets their desk scope covers: `DefaultTicketBoardService.LoadAsync` runs one query per column through `ITicketAccessService.ApplyScopeAsync`, at most 50 cards a column with the column's total. The board page (`Admin/tickets/board/{id}`) loads its cards from `Admin/tickets/board/{id}/data` (JSON) and again when tickets change: with SignalR on, `TicketBoardNotifier` (a ticket observer) pings the open boards through `TicketBoardHub` once a change is saved (one ping per request, carrying nothing about the ticket; each board reloads what its reader may see, and only users with a desk view scope may join), and a board that isn't connected loads again every 20 seconds while it is visible, so a card moves when the ticket's status changes elsewhere; its search and **My tickets** / **Unassigned tickets** filter stay in the address.

## Moving a card

A move (`POST Admin/tickets/board/{id}/move`, antiforgery) goes through `ITicketBoardService.MoveAsync`: the user needs the Edit permission on the ticket's desk, the ticket must still be in the status the board showed (otherwise the move is refused and the board reloads), and the status is set by `ITicketService.UpdateAsync` with the reason "Moved on the board X" on the timeline, so the life cycle, approval holds, observers, e-mails and workflows apply as on the ticket page. A refusal comes back with the ticket service's reason and the card goes back; a closed ticket's card can't be dragged.

## Permissions

| Permission | Lets a user |
| --- | --- |
| `ManageTicketBoards` (**Manage shared ticket boards**) | Create, arrange and delete boards shared with everybody or a team |
| `ManageOwnTicketBoards` (**Manage my own ticket boards**) | Create, arrange and delete their own, personal boards |

Opening a board needs no board permission, only the right to view tickets on some desk. Nobody arranges another user's personal board. Deleting a status doesn't touch boards: a column skips statuses that no longer exist, and its editor drops them. The feature adds a **Service desk** board, with a column per status category (closed left out), to a site that has none.

## Pinned boards

Each user may pin boards to their own admin menu (**Pin to menu** on the board and in the **Boards** list): `ITicketBoardPins` keeps them on the user record (`TicketBoardPinsPart` in the user's properties), and `TicketsAdminMenu` adds each pinned board the user may still see under **Service Desk**, after **Boards**, in the order pinned.
