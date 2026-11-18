---
sidebar_label: Permissions
title: Permissions
description: "Ticket permissions for each desk and over all desks, the three view scopes, the settings, board, report, protected information and approval permissions, and the one scope rule every page applies."
user_manual:
  - user-manual/service-desk/index
  - user-manual/service-desk/desks
---

A desk keeps its own tickets: every ticket permission exists once over all desks and once for each desk (such as **View all tickets in HR**). Viewing comes with a scope:

| Scope | Permission | Sees |
| --- | --- | --- |
| All | `ViewAllTickets` | Every ticket of the desk |
| My teams | `ViewTeamTickets` | The tickets of the user's teams and those assigned to them |
| Assigned | `ViewAssignedTickets` | The tickets assigned to them |

The other permissions (`EditTickets`, `ReplyToTickets`, `AddTicketNotes`, `AssignTickets`, `MergeTickets`, `DeleteTickets`) apply to the tickets the user may view. `ManageTicketSettings` covers statuses, priorities, desks, shared views and the e-mails (and implies `ManageTicketBoards`); `ViewTicketReports` opens the ticket reports. `TicketScopeFilter` is the one rule the list, the API, the data sets, the reports and the account tab apply, so a user never sees a ticket out of their scope anywhere.

| Other permission | Covers | Page |
| --- | --- | --- |
| `ManageTicketBoards`, `ManageOwnTicketBoards` | Shared and personal boards | [Boards](./boards#permissions) |
| `ViewProtectedTicketInformation` and `View protected information in {desk}` | Showing and removing protected items | [Protected information](./protected-information) |
| `CancelTicketApprovals` | Calling off an approval | [Approvals](./approvals) |

The Tickets Portal doesn't use desk permissions: a requester sees what they asked for, and an account administrator what their account asked for (see [Portal and web form](./portal-and-web-form)).
