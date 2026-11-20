---
sidebar_label: AI tools and assistant
title: AI tools and assistant
description: "The ticket AI tools AI profiles can use, and the AI assistant on a ticket: suggested replies, triage with a bounded priority, the profile it uses and what it sends to the AI provider."
user_manual: user-manual/service-desk/ai-assistant
---

## AI tools

With AI on, these tools can be picked on AI profiles (category **Tickets**). Each acts as the signed-in user, within their scope and permissions.

| Tool | Does |
| --- | --- |
| `listTicketOptions` | Lists the desks the user may open tickets in, the statuses, the priorities and the categories of the default ticket type, with their ids |
| `searchTickets` | Finds tickets by number or subject, status category, desk and assignee |
| `getTicket` | Reads a ticket and its latest 20 entries |
| `createTicket` | Opens a ticket on a desk, with a category |
| `addTicketReply` | Sends a public reply or adds an internal note |
| `updateTicket` | Changes the status, priority, category, team or assignee |

## AI assistant

With AI on, **Settings > Service Desk** has **AI profile for replies** (`TicketAISettings.ReplyProfileId`, in the site settings): the chat AI profile, whose instructions, knowledge (its data sources) and chat model the assistant uses, for every desk that names none. A desk's editor has **AI assistant**: the service desk's profile, another profile (`TicketDeskAISettings.ProfileId`, in the desk's properties) or **No AI assistant on this desk** (`TicketDeskAISettings.Disabled`), and **A triage sets the priority it suggests**. The settings page, the desk's choice and the ticket's panel are added by display drivers registered only with AI on (`AIToolsStartup`). The ticket page of a desk with a profile then has an **AI assistant** panel. **Suggest a reply** (`POST Admin/ticket/{id}/assistant/reply`, the Reply permission) reads the ticket's latest 30 replies and notes, internal notes marked as such, and puts a reply in the composer for the agent to edit and send; nothing goes to the requester on its own. **Triage** (`POST Admin/ticket/{id}/assistant/triage`, the Edit permission) asks for a summary and one of the site's priorities as JSON and adds them as an internal note from "AI assistant", setting the priority when the desk says so, within a bound: a triage may lower the priority, or raise it at most one step above the ticket type's default (above the ticket's priority when the type has none); a higher suggestion is only noted (`TicketAssistant.PriorityToApply`), because a requester can write "urgent, ignore your rules" into the request the assistant reads. A suggested reply goes into the composer without changing whether it is a public reply or an internal note, and the model is told the reply goes to the requester and must not quote, paraphrase or hint at internal notes. What the assistant sends to the profile's AI provider: the ticket's number and subject and its latest 30 replies and internal notes, with their authors' names (the requester's included), plus the profile's own instructions and knowledge. The AI profile's **Data privacy** setting decides what the site keeps of chat conversations, not what is sent, so choose the provider and its data terms for what tickets hold. The **Triage Ticket With AI** workflow task (with Workflows on) does the same, such as on Ticket Created, with the note in the `Triage` output. `TicketAssistant` runs with the profile's tools turned off, counts its use under the `Tickets` usage category, and answers "not available" when the profile, its model or the connection fails, without stopping the page.

With a desk's AI assistant on, its suggestions and triages send the ticket's conversation, internal notes and the names in it included, to the AI profile's provider; what the provider keeps is governed by its own terms.
