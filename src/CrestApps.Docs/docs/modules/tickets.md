---
sidebar_label: Tickets
title: Tickets
description: "The Tickets help desk: tickets as content items of any ticket type, desks with their own permissions, statuses, priorities, saved views and boards, replies and internal notes on the timeline, team assignment, service-level targets on business hours, e-mails, workflows, webhooks, the audit trail, data sets and reports, the JSON API, the account tab, personal data, AI tools and the recipe and deployment steps."
user_manual: user-manual/service-desk/index
---

**Tickets** is a help desk. A ticket is a content item of a ticket type, filed under a desk (such as IT, HR or customer support) that decides who may see and work on it. The admin menu is **Service Desk**: **Tickets** and **Boards**, and for those who manage it, **Desks**, **Statuses**, **Priorities** and **Views**; its site settings, the notification templates and (with AI on) the AI profile for replies, are under **Settings** → **Service Desk**. A ticket's page shows its **Conversation** (replies and internal notes, `Reply` and `Note` entries) apart from its **History** (every change: status, assignee, priority, links and the rest, `Event` entries), each paged on its own. Replies, internal notes and every change are kept on the ticket's [timeline](./timeline); first response and resolution targets run on [service levels](./service-levels) counted in [business hours](./business-hours); new tickets go to a team's members through [Teams](./teams).

| Feature | Id | Depends on |
| --- | --- | --- |
| Tickets | `CrestApps.OrchardCore.Tickets` | Timeline, Service Levels, Teams, Resources, Users, Contents, Title, Conditional Fields, Email Notifications (and so Notifications) |
| Tickets API | `CrestApps.OrchardCore.Tickets.Api` | Tickets |
| Tickets Portal | `CrestApps.OrchardCore.Tickets.Portal` | Tickets, My Account, Rate Limits |
| Ticket Web Form | `CrestApps.OrchardCore.Tickets.WebForm` | Tickets, Rate Limits |
| Ticket Approvals | `CrestApps.OrchardCore.Tickets.Approvals` | Tickets |
| Tickets for Contact Center | `CrestApps.OrchardCore.Tickets.ContactCenter` (its own module) | Tickets, Contact Center Work Distribution |

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.Tickets.Abstractions` | `TicketPart`, `TicketStatus`, `TicketPriority`, `TicketDesk`, `TicketView`, `TicketBoard`, `TicketBacklogDay`, the requests and results of the ticket service, `ITicketService`, `ITicketAccessService`, `ITicketBoardService`, `ITicketObserver`, `ITicketActionSource`, `ITicketReplyTemplateSource`, `TicketsPermissions` and `TicketsConstants` |
| `CrestApps.OrchardCore.Tickets.Core` | `TicketIndex` and `TicketBacklogIndex`, `DefaultTicketService`, `DefaultTicketAccessService`, `DefaultTicketBoardService`, `TicketBoardColumns`, `TicketScopeFilter`, `TicketLifecycle`, the auto closer, the backlog recorder, the e-mail observer and template source, the team load source, the timeline, record and service-level timer access, and the API models (`TicketApiModel`, `TicketApiMessages`) |
| `CrestApps.OrchardCore.Tickets` (module) | The pages, the display drivers, the background tasks, the workflow activities, the webhooks and audit trail, the data sets and reports, the API, the account tab, the personal data contributor, the AI tools, the migrations and the recipe and deployment steps |

## The Tickets pages

| Page | Covers |
| --- | --- |
| [Desks and settings records](./tickets/desks-and-settings) | Desks and their settings, statuses and status categories, priorities and the priority matrix, saved views, **Settings** → **Service Desk** |
| [Ticket types and categories](./tickets/ticket-types-and-categories) | Ticket types and `TicketPartSettings`, the New ticket page, the Details card, categories and the questions that follow them |
| [Assignment and life cycle](./tickets/assignment-and-life-cycle) | The ticket service, assignment and availability, the background tasks, planning values, presence, problems and incidents |
| [Permissions](./tickets/permissions) | Desk permissions and view scopes, the other permissions, `TicketScopeFilter` |
| [Boards](./tickets/boards) | Boards, columns, moves, pins and their permissions |
| [Protected information](./tickets/protected-information) | Encrypted secrets on a ticket, showing them, requesters and life cycle |
| [Service levels](./tickets/service-levels) | The `Tickets` scope, its metrics and where a ticket's policy comes from |
| [E-mails and notifications](./tickets/emails-and-notifications) | Requester messages and their templates, the outbox, the notification types, reply templates |
| [Portal and web form](./tickets/portal-and-web-form) | The Tickets Portal, ratings, requester links and the Ticket Form widget |
| [Approvals](./tickets/approvals) | Approval steps before work goes on |
| [Workflows](./tickets/workflows) | Workflow events and tasks |
| [Webhooks and audit trail](./tickets/webhooks-and-audit-trail) | Webhook events and audit trail records |
| [AI tools and assistant](./tickets/ai) | The AI tools for profiles and the ticket's AI assistant |
| [Data sets and reports](./tickets/data-sets-and-reports) | The **Tickets** data source and the ticket reports |
| [API](./tickets/api) | The `api/tickets/v1` endpoints |
| [Integrations](./tickets/integrations) | Accounts, Agreements, Work Orders, Time Tracker, messaging, Contact Center routing and personal data |
| [Storage](./tickets/storage) | The `ServiceDesk` collection and the indexes |
| [Recipes and deployment](./tickets/recipes-and-deployment) | The recipe and deployment steps and the **Help desk starter** recipe |
| [Extending Tickets](./tickets/extending) | The extension points for other modules |

The help center that sits beside the help desk is the [Knowledge Base](./knowledge-base).
