---
sidebar_label: SMS Workspace
sidebar_position: 4
title: SMS Workspace (Human Two-Way SMS)
description: A human-operated two-way SMS inbox, conversation workspace, and broadcast console for Orchard Core.
---

| | |
| --- | --- |
| **Feature Name** | SMS Workspace |
| **Feature ID** | `CrestApps.OrchardCore.Sms.Workspace` |
| **Category** | Communication |

The **SMS Workspace** is a **human-operated** two-way SMS portal: an inbox, threaded conversations, a composer, canned-response templates, and 1:1 broadcasts, staffed by real operators.

It is the human counterpart to [SMS Automation](sms), which lets an **AI agent** carry an SMS conversation on its own. Both build on the same Omnichannel foundation and can run side by side on the same numbers — an automated activity handles a conversation until it hands off, after which the human workspace owns the thread.

## Overview

The workspace turns a set of SMS numbers into a shared team inbox:

- **Conversations** — one thread per customer phone number, hydrated from the shared Omnichannel message store. A customer always maps to a single conversation, so replies never fork into duplicate threads.
- **Composer** — send from any of your numbers, insert a canned-response **template**, and link the thread to a **customer** (any content item that uses the Omnichannel Contact part).
- **Live contact search** — the *To* selector searches your Contact content items by phone number, first name, and last name, so operators can start a conversation from a known contact or a raw number.
- **Claim, assign, transfer** — pull an unassigned conversation into your own queue, hand it to another operator, or move it between personal and queue ownership.
- **Close / spam / reopen** — resolve conversations, flag spam, and reopen when a customer replies.
- **Broadcasts** — send one message to many recipients as individual 1:1 threads (not a group chat), processed by a durable, resumable background task.
- **Real-time updates** — inbound messages and delivery receipts are pushed to open inboxes over the workspace's own SignalR hub.

## How it fits with the other modules

The workspace deliberately **reuses existing building blocks** instead of duplicating them:

| Concern | Reused from | Notes |
| --- | --- | --- |
| Number, provider, and inbound routing | [Omnichannel Channel Endpoints](management#channel-endpoint) | Each SMS number is a **channel endpoint**. The provider and the SMS routing (agent/queue target, distribution mode, auto-reply) are edited on the endpoint itself. |
| Message store | Omnichannel `OmnichannelMessage` | Human and automated messages share one store and one number catalog, which is what makes AI↔human hand-off seamless. |
| Operator identity | Contact Center **Agent Services** (dependency-only) | Operators are Contact Center **agent profiles**, resolved through the shared agent-profile directory. A bare profile is provisioned automatically on first workspace access, so no Contact Center administration is required. When [Contact Center](../contact-center/agents-queues-dialer) is also enabled, the same profile gains queues and entitlements, and the "my conversations" view is scoped by the agent's queue memberships. |
| Sending & provider integration | Orchard Core **SMS** (`OrchardCore.Sms`) | Providers such as [Telnyx SMS](../telephony/telnyx#telnyx-sms) and Twilio plug in here. |

### Dependencies (and why they are minimal)

Enabling **SMS Workspace** pulls in only what it actually uses:

- **Omnichannel Channel Endpoints** (`CrestApps.OrchardCore.Omnichannel.ChannelEndpoints`) — a small [dependency-only feature](management#channel-endpoint) that exposes the channel-endpoint administration and services **without** the full Omnichannel Management CRM screens (campaigns, subjects, dispositions, load inventory).
- **Contact Center Agent Services** (`CrestApps.OrchardCore.ContactCenter.AgentServices`) — a small **dependency-only** feature that provides just the shared agent-profile directory (store, manager, index). It carries **no** administration screens, so enabling the workspace does **not** pull in the Contact Center Agents or Work Distribution admin (agent states, entitlements, business hours, queues, skills). Operator profiles are provisioned automatically on first workspace access.
- **Orchard Core SMS** (`OrchardCore.Sms`) — the provider abstraction and the site SMS settings (default provider).
- **Orchard Core SignalR** — transport for the workspace's own real-time hub.

It does **not** require Contact Center **Voice**, **Work Distribution**, or **Agents administration**, it does not use the Contact Center real-time hub (it ships its own), and it does not pull in the Omnichannel Management CRM.

If you also enable the full Contact Center features, the workspace integrates with them automatically — the same operator profiles gain queues, entitlements, and presence, and queue-target SMS routing becomes meaningful.

## Enable the feature

1. Go to **Tools → Features** in Orchard Core.
2. Enable **SMS Workspace**. Its dependencies (Channel Endpoints, Agent Services, SMS, SignalR) are enabled automatically.

## Setup

1. **Configure an SMS provider.** Enable and configure at least one provider — for example [Telnyx SMS](../telephony/telnyx#telnyx-sms) or Twilio — and pick the tenant **default provider** at **Settings → SMS** (`/Admin/Settings/sms`). The workspace does **not** define its own default-provider setting; it uses Orchard Core's.
2. **Register your numbers as channel endpoints.** In **Interaction Center → Channel Endpoints**, add an **SMS** endpoint for each number you send from. Numbers are normalized to the international `+<country code><number>` format.
3. **Set the SMS routing on the endpoint.** On the SMS endpoint's editor, use the **SMS routing** section to choose:
   - **Target** — an **agent** (personal number) or a **queue** (department number).
   - **Distribution mode** — **Shared pool** (operators claim conversations to own them) or **Routed** (assigned through a routing strategy).
   - **Auto-reply** — an optional message sent on the first inbound message.
4. **Grant operator permissions.** Assign the workspace permissions to the roles that will staff the inbox. A bare operator (agent) profile is created automatically the first time a permitted user opens the workspace — no separate onboarding step is required. (If you also run the full Contact Center, manage richer entitlements there.)
5. **Point the provider webhook at Orchard Core** so inbound messages and delivery receipts are delivered (see the provider's own documentation — e.g. the [Telnyx SMS webhook](../telephony/telnyx#telnyx-sms)).
6. **Open the workspace** from the admin menu and start handling **Conversations**.

## Routing lives on the channel endpoint

Earlier iterations used a separate "number route" entity; that has been retired. All SMS routing — target, distribution mode, and auto-reply — is stored **on the channel endpoint** and edited on the same screen as the number and its provider. Inbound routing resolves in order: an existing conversation wins first, then the endpoint's routing, then a fallback into the unassigned inbox.

## Per-number provider dispatch

Sending is handled by an `ISmsDispatcher` that resolves the number's provider from its channel endpoint, falls back to the Orchard Core SMS default provider, and dispatches through the matching `ISmsProvider`. This is what lets different numbers on the same tenant send through different carriers.

## Opt-out handling

Inbound opt-out keywords such as `STOP` close the conversation and update the contact's **Do not SMS** preference, consistent with the automated SMS path.

## Permissions

The workspace ships its own permission set (using the SMS Workspace inbox, managing templates and broadcasts, and supervisor access to all conversations). Assign them to the roles your operators and supervisors use.
