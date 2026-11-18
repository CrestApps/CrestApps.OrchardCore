---
sidebar_label: AI Assistant
title: Let the AI Assistant Help
description: "Let an AI profile suggest replies and triage tickets on a desk, with what it may change and what it sends to the AI service."
technical_manual:
  - modules/tickets/ai
---

With AI turned on, an AI profile can draft replies for your agents and triage new tickets.

| | |
| --- | --- |
| **Menu** | Settings > Service Desk; Service Desk > Desks for each desk |
| **Permission** | Manage ticket settings to choose the profile; **Reply to tickets** for **Suggest a reply**; **Edit tickets** for **Triage** |
| **Feature** | Tickets, with AI |

<AskYourAdmin />

## Set it up

1. Go to **Settings > Service Desk** and choose the **AI profile for replies**: a chat AI profile with the instructions and knowledge your agents should answer from. See [AI profiles](../ai/profiles.md).
2. Every desk uses it, unless you edit a desk and choose another profile under **AI assistant**, or **No AI assistant on this desk**.
3. On the desk, tick **A triage sets the priority it suggests** to have a triage change the priority too.

## Use it on a ticket

Tickets on a desk with a profile show an **AI assistant** panel:

- **Suggest a reply** writes a reply into the reply box from the conversation. Read it, change it, then send it; nothing is sent until you do, and it stays a public reply or an internal note as you had chosen.
- **Triage** adds an internal note with a short summary and a suggested priority. When the desk lets it, it may lower the priority, or raise it at most one step above the ticket type's default; anything higher is only noted for an agent, so a requester can't talk their way to the top.

When the profile, its model or the connection fails, the panel says the assistant isn't available, and the rest of the page works as usual.

## What it sends

The assistant sends the ticket's number and subject and its latest 30 replies and internal notes, names included, to the AI service of the profile you chose. Pick a service whose data terms fit what your tickets hold. [Protected information](./protected-information.md) is never sent.

## Triage every new ticket

To triage every new ticket, add the **Triage Ticket With AI** task to a workflow that starts with **Ticket Created**. See [Workflows](../workflows.md).
