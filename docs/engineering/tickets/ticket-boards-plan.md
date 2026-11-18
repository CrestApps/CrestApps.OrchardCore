# Ticket boards

A board shows tickets as cards in columns. Each column holds one or more ticket statuses, and dragging a card to another
column sets the ticket's status. This plan is one branch (`ma/ticket-boards`, started from `ma/tickets-phase-2`) and one
pull request, with a commit per slice.

## What Mike asked for

- **Columns.** The owner picks the statuses of each column; several statuses can share a column. Columns are added,
  removed, renamed and reordered.
- **Moving cards.** A drop sets the ticket's status to the column's status. A column with several statuses either has a
  chosen default or asks which one. A card moves when its status changes elsewhere.
- **Many boards.** Limited to desks, teams or ticket types; shared or personal.
- **Permissions.** A permission controls who creates and arranges boards. A board only ever shows tickets the user may
  already see.

## Reuse (building blocks)

| Concept | Owner | How boards use it |
| --- | --- | --- |
| Tickets, statuses, the life cycle, desk permissions | Tickets | A move calls `ITicketService.UpdateAsync`; the board query goes through `ITicketAccessService.ApplyScopeAsync` |
| Which tickets (desks, teams, types, priorities, assignee) | Tickets (`TicketQuery`) | The board keeps a `TicketQuery`, as a saved view does; the columns supply the statuses |
| Shared, team or personal records | Tickets (`TicketView`) | The same `OwnerId` / `SharedWithTeamId` pair, now behind `ITicketSharedRecord`, so the owner travels by user name in recipes for both |
| Teams | Teams | `ITeamMembershipService` for "shared with my team"; the `TeamPicker` view component in the editor |
| Records, recipes, deployments | CrestApps.Core catalogs | `ICatalog<TicketBoard>`, `TicketSettingsRecordHandler<T>`, `CatalogDeploymentSerializer`, the same recipe and deployment step shape as views |
| Admin list, editor buttons | Orchard Core | `<admin-list>`, rows from a display driver, `action-bar-bottom` |
| Drag and drop | Orchard Core resources | SortableJS (`Sortable` resource), as the product listing editor uses it; it also works on touch screens |
| Live updates | (none) | The board reloads its data every 20 seconds while the page is visible |

**No existing board or drag-and-drop building block.** The Dispatch board is a timeline per technician, not columns of
cards. Opportunities have stages but no board. So the board is kept in Tickets for now (reuse ladder step 5), with the
script written against a data contract (columns, cards, a move endpoint) and no ticket logic in the drag code. When a
second module (work orders, opportunities) wants a board, step 4 applies: move the column model to a `Boards.Abstractions`
and the script to Resources as a generic board shell, and point both modules at it in the same plan.

## Design

- `TicketBoard` (catalog record, Tickets.Abstractions): name, description, `Columns`, `Query`, `OwnerId`,
  `SharedWithTeamId`, `Order`, time stamps. `TicketBoardColumn`: id, name, `StatusIds`, `DefaultStatusId`, `Limit` (a
  work-in-progress limit, shown on the header; it never refuses a move).
- Rules (`TicketBoardHandler`): at least one column, at most 20; each column has a name and a status; a status is in one
  column only; the default is one of the column's statuses; statuses must exist. Column ids are given when missing.
- Visibility: shared with everybody (no owner, no team), with a team, or personal (owner). Viewing needs a ticket view
  scope on at least one desk, as the ticket list does.
- Permissions: **Manage shared ticket boards** (`ManageTicketBoards`, implied by `ManageTicketSettings`) creates and
  arranges boards for everybody or a team; **Manage my own ticket boards** (`ManageOwnTicketBoards`, implied by the
  first) creates and arranges personal boards.
- `ITicketBoardService` (Core): visible boards, can-manage, the columns' tickets (one scoped query per column, at most
  50 cards each, with the total), and the move: column → status (the given one, the default, the only one, or "ask"),
  a stale check against the status the board showed, the user's Edit permission on the ticket, then `UpdateAsync` with
  the reason "Moved on the board X". `UpdateAsync`'s refusals (life cycle, approval holds) come back as the move's
  errors and the card goes back.
- Deleting a status is not blocked by boards: a board skips statuses that no longer exist.
- Pages: Service Desk → Boards (`tickets/boards`, every board the user may open), the editor, and the board
  (`tickets/boards/{id}`, with a JSON data endpoint and a move endpoint).
- Recipe step `TicketBoards`, deployment step **Ticket boards**, and a board in the help-desk starter recipe.

## Slices

1. Model, rules, permissions, service, tests.
2. Pages: list, editor (columns editor script), board (board script), menu, vitest.
3. Recipe and deployment steps, starter recipe.
4. Docs (module page, user manual, changelog), browser test.

## Follow-ups

- SignalR push instead of polling, as the Dispatch board does when SignalR is on.
- Swimlanes (rows by assignee or priority).
