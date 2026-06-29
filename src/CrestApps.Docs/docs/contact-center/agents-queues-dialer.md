---
sidebar_label: Agents, Queues & Dialer
sidebar_position: 1
title: Agents, Queues, and Dialer
description: Phase 2 and 5 of the Contact Center - agent presence, queues, reservations, availability-based assignment, and dialer-agnostic outbound dialing with the DialPad provider.
---

This phase adds the operational core of the Contact Center: agent presence, work queues,
reservations, availability-based assignment, and a dialer-agnostic outbound dialer. Each capability
is a separate, feature-gated module so tenants enable only what they need.

## Features

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Contact Center Agents | `CrestApps.OrchardCore.ContactCenter.Agents` | Agent profiles, presence, capacity, skills, and queue/campaign sign-in. |
| Contact Center Queues | `CrestApps.OrchardCore.ContactCenter.Queues` | Work queues, queue items, reservations, and availability-based assignment. |
| Contact Center Dialer | `CrestApps.OrchardCore.ContactCenter.Dialer` | Dialer-agnostic outbound profiles, pacing, and dialer activity batches. |
| DialPad Dialer | `CrestApps.OrchardCore.DialPad.Dialer` | DialPad implementation of the dialer-agnostic provider. |

## Agents and presence

An **agent profile** links an Orchard user to Contact Center configuration: display name, capacity,
skills, queue membership, campaign membership, and live presence. Presence states are `Offline`,
`Available`, `Reserved`, `Busy`, `WrapUp`, and `Break`.

Agents sign in from **Contact Center → Agent Workspace**, selecting the queues and campaigns they
want to receive work from. Signing in sets presence to `Available`; signing out sets it to `Offline`.
The `SignIntoQueues` permission grants self-service sign-in and presence changes.

## Queues, reservations, and assignment

A **queue** holds activities waiting for an agent, with a default priority, an SLA threshold, and a
reservation timeout. Activities enter a queue as **queue items**; the system pairs the highest
priority, oldest waiting item with the **longest-idle available agent** who is signed in to that
queue and creates a short-lived **reservation**.

A reservation locks the activity for one agent and can be accepted, rejected, or expired. The CRM
activity moves through `Available → Reserved → Assigned`, mirrored on the queue item and agent
presence. Expired reservations return the item to the queue automatically. A background task expires
stale reservations and assigns waiting work every minute.

## Dialer

A **dialer profile** ties a campaign and queue to a dialing mode (`Manual`, `Preview`, `Power`,
`Progressive`, `Predictive`), a provider, calls-per-agent pacing, and attempt limits. Power and
progressive profiles run automatically each minute: the Contact Center reserves an available agent,
creates an outbound interaction, and asks the configured provider to place the call. Manual and
preview profiles wait for agent action. Dialer activity batches load **unassigned** inventory the
dialer reserves later.

## Dialer-agnostic providers

The dialer never talks to a telephony platform directly. It calls `IDialerProvider` and resolves the
configured provider through `IDialerProviderResolver`, so any platform can be the calling engine
while the Contact Center keeps all assignment, queue, pacing, and compliance logic. The
`DialPad.Dialer` feature implements `IDialerProvider` over the DialPad telephony provider; enable it
to dial through DialPad.

## Enable via recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContactCenter",
        "CrestApps.OrchardCore.ContactCenter.Agents",
        "CrestApps.OrchardCore.ContactCenter.Queues",
        "CrestApps.OrchardCore.ContactCenter.Dialer",
        "CrestApps.OrchardCore.DialPad.Dialer"
      ]
    }
  ]
}
```
