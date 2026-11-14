# CrestApps Tickets

A help desk for Orchard Core. Tickets are content items of any ticket type (a content type with `TicketPart`), filed under desks that decide who may see and work on them. Replies, internal notes and every change are kept on the ticket's timeline; first response and resolution targets run on service levels counted in business hours; new tickets go to a team's members.

| Feature | Id |
| --- | --- |
| Tickets | `CrestApps.OrchardCore.Tickets` |
| Tickets API | `CrestApps.OrchardCore.Tickets.Api` |

What it adds:

- **Tickets** list on the admin list (saved views, search, filters, bulk actions), the ticket page with its thread and composer, **New ticket** on behalf of a requester, merge and links.
- **Configuration > Tickets**: desks, statuses, priorities, shared views and the requester e-mails.
- Permissions over all desks and for each desk, with a view scope of all tickets, my teams' tickets or my assigned tickets.
- Team assignment by the fewest open tickets or in turn, service-level timers, auto-close of solved tickets and a daily backlog snapshot.
- With other features on: workflow events and tasks, webhooks, audit trail events, the **Tickets** data source and five reports, a **Tickets** tab on accounts, personal data export and erasure, AI tools and the `api/tickets/v1` JSON API.
- Recipe and deployment steps for statuses, priorities, desks and views, and the **Help desk starter** recipe.

Documentation: [Tickets](https://orchardcore.crestapps.com/docs/modules/tickets) (technical) and [Tickets](https://orchardcore.crestapps.com/docs/user-manual/administration/tickets) (user manual).
