---
sidebar_label: Extending Tickets
title: Extending Tickets
description: "The extension points other modules use: ticket observers, actions and reply templates on the ticket page, panels, the ticket and access services, and the board service."
---

| To | Use |
| --- | --- |
| React to ticket changes | `ITicketObserver` (created, updated, assigned, status changed, replied, merged, deleted, service level) |
| Offer actions on the ticket page | `ITicketActionSource` |
| Offer reply templates in the composer | `ITicketReplyTemplateSource` |
| Add a panel to the ticket page | A `ContentPartDisplayDriver<TicketPart>` or a display driver of the ticket type |
| Read or change tickets | `ITicketService` and `ITicketAccessService`; apply `TicketScopeFilter` to your own ticket queries |
| Read a board or move a card from code | `ITicketBoardService` (the visible boards, `LoadAsync`, `MoveAsync`); add fields to the board editor with a `DisplayDriver<TicketBoard>` |
