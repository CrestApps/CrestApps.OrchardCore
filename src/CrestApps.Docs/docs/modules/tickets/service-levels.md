---
sidebar_label: Service levels
title: Service levels
description: "Tickets as a service-level scope: its metrics, what starts, pauses and completes each target, and where a ticket's policy comes from: its agreement, account, category, type or desk."
user_manual:
  - user-manual/service-desk/desks
  - user-manual/service-desk/answer-a-ticket
---

Tickets is a [service-level](../service-levels) scope named `Tickets`, with the metrics `FirstResponse`, `NextResponse` and `Resolution` and the priorities by name. A new ticket takes the policy of its account, then its type, then its desk. The first public reply from an agent completes the first response; each customer reply starts a next response; solving completes the resolution; a status that pauses timers pauses them. `TicketPart.NextDueUtc` keeps the next due time for the list and the reports.

The full order a new ticket's policy is chosen in is its agreement's (see [Integrations](./integrations#agreements)), then the account's, then the category's (see [Ticket types and categories](./ticket-types-and-categories#categories)), the type's and the desk's.

## Accounts

With [Accounts](../accounts) on, an account's **Ticket service level** (`TicketAccountPart`, on the `Account` type) names the policy its tickets run on, before the ticket type's and the desk's, for a premium support plan, say. With **Account E-mail Domains** on, a new ticket without an account is filed under the account whose domain its requester's e-mail is on, unless the address is unconfirmed (a guest's on the web form), since anyone could type a customer's address.

The account's **Service level** card on its **Service** tab is described in [Integrations](./integrations#accounts).
