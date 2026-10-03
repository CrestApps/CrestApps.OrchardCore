---
sidebar_label: Channel Endpoints
sidebar_position: 15
title: Channel Endpoints
description: Register the phone numbers and SMS numbers you own, choose the SMS provider for each, and route inbound messages to an agent or a queue.
---

A **channel endpoint** is one address you own on a channel: a phone number for calls, or a number for SMS. Endpoints are what automated inbound subjects answer on, what automatic SMS loads send from, and where the Messaging workspace routes incoming texts.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Channel Endpoints |
| **Permission** | Manage channel endpoints |
| **Feature** | Omnichannel Management. The **Phone** channel comes with Contact Center Inbound Voice; the **SMS** channel comes with SMS Messaging Channel. |

<video controls preload="metadata" width="100%" aria-label="Screencast of adding an SMS channel endpoint with its provider and inbound routing">
  <source src="/img/docs/um-channel-endpoints.mp4" type="video/mp4" />
</video>

## Add an endpoint

1. Open **Interaction Center > Management > Channel Endpoints** and click **Add Channel Endpoint**.
2. Pick the channel (**Phone** or **SMS**) in the *Available Channels* window.
3. Enter a **Name** people will recognize, the **Endpoint value** (the number, in international format such as `+17025550100`), and an optional **Description**.
4. For SMS, pick the **Provider**, or leave *Use the tenant-default provider*.
5. Click **Save**.

Phone and SMS values must be valid international numbers. The channel cannot be changed after the endpoint is created, and endpoints cannot be deleted.

The endpoint list shows each endpoint's source (**Phone** or **SMS**), its number and its provider as badges under its name.

## Inbound routing for phone numbers

When Contact Center Inbound Voice is enabled, Phone endpoints show an **Inbound routing** section:

| Field | What it does |
| --- | --- |
| **Entry point** | The [entry point](entry-points-and-ivr.md) that answers calls to this number. The entry point decides the queue or agent, the opening hours, the phone menu and voicemail. |

Leave it on **None** to keep routing the number as before: by the entry point that lists it under **Dialed numbers**, or else by the queue mapped to this endpoint. When an entry point is chosen here, it wins over an entry point that lists the number. The section names the entry points that also list the number, so you can tidy them up.

## Inbound routing for messaging

When the Messaging workspace is enabled, SMS endpoints show an **Inbound routing** section:

| Field | What it does |
| --- | --- |
| **Routes to** | **Agent** (a personal number: texts go to one person) or **Queue** (a department number). |
| **Agent** | The person who owns the personal number. |
| **Queue (department)** | The queue whose shared inbox receives the texts. |
| **Queue distribution** | **Shared pool (claim to own)**: any agent in the queue can claim a conversation. **Routed (assign via routing strategy)**: each new conversation is assigned to an available agent. Routed needs the *Omnichannel Messaging Routed Distribution* feature. |
| **Auto-reply** | A message sent back automatically, at most once a day per customer. |

See [Messaging workspace](messaging.md).
