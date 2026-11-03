---
sidebar_label: Contact Memory and Integrations
title: Contact Memory and Integrations
description: Let the AI remember each contact across calls, texts and chats, send signed webhooks to other systems, and connect calendars and CRMs with workflows.
technical_manual:
  - ai/contact-memory-and-integrations
---

The AI can remember each customer from one conversation to the next, on every channel. Workflows send what happened to your other systems, signed so they can trust it, and connect the AI to your calendar and your CRM.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > AI Profiles, Design > Workflows, and your contacts |
| **Permission** | Manage AI profiles, Manage workflows, and edit contacts |
| **Feature** | AI Services and Omnichannel Management. Workflows for the webhooks and integrations. |

<AskYourAdmin />

## Contact memory

Each contact can have a short note, up to 2,000 characters, of what the AI remembers about them: preferences, facts that are still true, and anything still open, such as a refund they are waiting for. The note is shared by every AI profile and every channel, so a customer who texted last week is remembered when they call.

Turn it on for each AI profile, on the **Data Processing & Metrics** tab:

| Option | What it does |
| --- | --- |
| **Use contact memory** | When a call or a text is with a known contact, the AI starts with what it remembers about them. |
| **Save to contact memory** | When a conversation with a known contact ends, the note is rewritten from it. Resolved matters are dropped and new ones added. |

The note is rewritten when the conversation is closed: a phone call's as soon as the call ends, and a text or chat conversation's after the profile's session inactivity timeout (30 minutes by default). The post-session analysis and the **AI Chat Session Closed** workflow event run at the same moment. Website chats with anonymous visitors have no contact, so they neither read nor write memory.

Memory works the same for leads: a call or text with a lead reads and writes the lead's note. When the lead is converted, its note moves to the contact it became, or is added to the note of the contact it was merged into.

To see or correct a note, open the contact. **What the AI remembers** shows the note: edit it, or empty it to make the AI forget.

:::note[Payment details and secrets]
The note is written by the utility model, which is told never to keep card numbers, passwords or other secrets. Still, check the notes of profiles that handle sensitive matters, and leave **Save to contact memory** off where it is not needed.
:::

### Update memory from a workflow

The **Update Contact Memory** task (under **AI Conversations**) rewrites a contact's note from a finished conversation, whatever the profile's own setting. Use it when only some conversations should be remembered, for example after an **If/Else** on the disposition. **Conversation** defaults to the session the event was raised for, and **Contact** to the contact the call or text was with.

## Send signed webhooks

The **Send Signed Webhook** task (under **HTTP**) posts JSON to another system, signed with a secret you share with that system, so it can check that the request came from your site and is not an old one sent again.

| Field | What to write |
| --- | --- |
| **URL** | The address to post to. |
| **Event name** | A name the receiver uses to tell your webhooks apart, such as `conversation.evaluated`. |
| **Body** | The JSON to send, as Liquid. The default sends everything the workflow started with. |
| **Signing secret** | A long random string, given to the receiver too. It is kept encrypted and never shown again; leave it empty to keep it. |
| **Attempts** | How many times to try when the receiver times out, is busy or fails, a few seconds apart. |

Each request carries these headers, the same as the commerce webhooks:

| Header | Holds |
| --- | --- |
| `X-Webhook-Id` | A unique ID for the delivery, the same on every attempt. |
| `X-Webhook-Event` | The event name. |
| `X-Webhook-Timestamp` | When it was signed, in seconds since 1970. |
| `X-Webhook-Signature` | `v1=` followed by the HMAC-SHA256, in lowercase hex, of the timestamp, a dot and the body, keyed with the secret. |

The receiver computes the same signature and compares, and refuses a timestamp more than a few minutes old.

The task goes on by **Delivered** or **Failed**. What happened is in `Workflow.Output.Webhook`: its `StatusCode`, `Attempts` and `Error`.

For example, start a workflow with **AI Conversation Evaluated**, tick **Only conversations that failed**, and add **Send Signed Webhook** with **Event name** `conversation.failed_qa` to tell your QA tool.

## Calendars

Workflows connect the AI to a booking service that has an API, such as Cal.com, Calendly or your own system:

1. Create a workflow that starts with **AI Tool Called**, with an **HTTP Request** to the service's *available times* API, using `{{ Workflow.Input.Arguments.date }}`, and **Set Output** with the times it returns.
2. Create a second one that books a time from `{{ Workflow.Input.Arguments.time }}` and the customer's details.
3. Create a **Workflow** tool instance for each (see [Automate AI conversations](conversation-workflows.md)), described as *Finds free appointment times on a date* and *Books an appointment at a time the customer chose*, and add both to the AI profile.

Keep the service's API key in the HTTP Request's headers. Built-in connections to Google Calendar and Microsoft 365 are not available yet.

## CRMs

The same way, workflows keep a CRM such as HubSpot or Salesforce up to date:

- **Look a caller up before the AI answers**: in an **AI Conversation Starting** workflow, an **HTTP Request** to the CRM's search API with `{{ Workflow.Input.CustomerAddress }}`, then **Set Conversation Variables** with what it returns.
- **Let the AI update a record**: a **Workflow** tool instance whose workflow calls the CRM's update API.
- **Log the conversation**: an **AI Chat Session Post-Processed** workflow with an **HTTP Request** that creates a CRM activity with the summary and the transcript.
- **Start an AI call from the CRM**: the CRM's own automation calls an **HTTP Request Event** workflow that runs **Create AI Activity**.

Built-in CRM connectors, with field mappings and scheduled imports, are not available yet.
