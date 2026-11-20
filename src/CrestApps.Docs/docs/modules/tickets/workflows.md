---
sidebar_label: Workflows
title: Workflows
description: "The workflow events tickets raise, with their filters and input, and the workflow tasks that create, change and read tickets."
user_manual: user-manual/service-desk/answer-a-ticket
---

With Workflows on, these events start workflows. Each can be narrowed to desks, ticket types and categories (a category takes the categories below it along, by the ticket's `CategoryPath`); the ticket's values are in `Workflow.Input` (`TicketId`, `Number`, `Subject`, `DeskId`, `StatusId`, `StatusCategory`, `PriorityId`, `TeamId`, `AssigneeId`, `Requester`, `ContentType`) with what the event adds (`ChangedFields`, `Previous`, `Body`, `Metric`, `MergedIntoId`, `ActionName`). `ChangedFields` and the changed-field filter use stable keys (`TicketChangeKeys`: `Subject`, `Desk`, `Status`, `Priority`, `Team`, `Assignee`, `EscalationLevel`, `Link`), not the shown labels, which are translated; each change also carries the ids it moved from and to (`FromValue`, `ToValue`). A workflow starts after the change is committed and the ticket's lock is released, so it reads the saved ticket.

| Event | When |
| --- | --- |
| Ticket Created, Ticket Updated (with a changed-field filter), Ticket Status Changed (with a category filter), Ticket Assigned | The ticket changes |
| Ticket Customer Replied, Ticket Agent Replied, Ticket Note Added | An entry is added |
| Ticket Service Level Warning, Ticket Service Level Breached | A target is close to or past due |
| Ticket Solved, Ticket Reopened, Ticket Closed, Ticket Merged | The life cycle moves |
| Ticket Action Requested | An agent runs a named action from the ticket's **Actions** menu |
| Ticket Time Elapsed | A time passes, in clock or business hours, since creation, the last customer or agent reply, or the last status change |

Tasks: Create Ticket, Update Ticket, Assign Ticket, Add Ticket Reply, Add Ticket Note, Escalate Ticket, Get Ticket, Find Tickets, Check Ticket Service Level, Link Tickets and Notify about a Ticket, and, with Work Orders on, Create Work Order From Ticket and, with AI on, Triage Ticket With AI. Changes a workflow makes are marked, and a chain of workflow-caused changes stops starting workflows at depth 3, so rules can't loop.

The **Ticket Rated** event, with Tickets Portal ratings, is described in [Portal and web form](./portal-and-web-form#ratings).
