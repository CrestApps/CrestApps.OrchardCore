---
sidebar_label: Webhooks and audit trail
title: Webhooks and audit trail
description: "The webhook events tickets send to subscribers and the security-relevant ticket events the audit trail records."
---

With Webhooks on, subscribers get `ticket.created`, `ticket.updated`, `ticket.assigned`, `ticket.reply_added` (public replies only; internal notes never leave the site), `ticket.solved` and `ticket.closed`, with the ticket as the API gives it. With Audit Trail on, the security-relevant events are recorded with who did it (the agent, or a workflow): a ticket moved to another desk, merged, or deleted, with ids only (`TicketAuditData`), never contact details. Everyday changes are already on the ticket's timeline.
