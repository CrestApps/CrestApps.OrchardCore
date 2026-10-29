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

To build a note from a contact's past conversations, for example after turning memory on, tick **Rebuild from past conversations** on the contact and save. The note is written again from the contact's past AI calls, texts and chats, oldest first, as if each had just ended (at most the 50 oldest). It runs after the save, so reload the contact a moment later to see it.

What the AI keeps in a note follows built-in instructions. To change them, for example to keep only open orders or to write the note in another language, go to **Settings > Artificial Intelligence** and write your own **Memory instructions** under **Contact memory**. Leave them empty to go back to the built-in ones. The note is kept to 2,000 characters either way.

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

## Webhook subscriptions

With the **AI Webhooks** feature on, **Artificial Intelligence > Webhooks** sends AI events to another system without a workflow. Click **Add webhook**, enter the **URL** (HTTPS) and a **Signing secret**, and tick the events to send:

| Event | Sent when |
| --- | --- |
| `conversation.started` | An AI conversation starts, on any channel. |
| `conversation.ended` | An AI conversation ends. |
| `conversation.analyzed` | A conversation's analysis finishes, with its extracted fields and results. |
| `conversation.transferred` | An AI call is handed to a person or transferred to a number. |
| `conversation.evaluated` | A conversation is scored, by a QA cohort, a test or a workflow. |
| `alert.triggered` | An AI alert rule crosses its threshold. |
| `alert.resolved` | An AI alert rule is back within its threshold. |

The body is JSON: `{ "id": "...", "event": "conversation.ended", "createdUtc": "...", "data": { ... } }`, signed with the same headers as the **Send Signed Webhook** task below. Events are queued and sent within a minute, so a slow receiver never holds up a conversation.

Click **Deliveries** to see what was sent: each event with its body, the receiver's answer and the number of attempts. A delivery that fails with a timeout, 429 or a 5xx answer is tried again after 1, 5 and 30 minutes, then 2 and 12 hours; any other refusal is not retried. **Send again** queues a delivery once more, and **Send a test event** sends `webhook.test` to check the receiver.

## Integrations

Enable **AI Integrations** to connect the AI to a CRM, Google Drive, Microsoft 365 or Notion. Under **Artificial Intelligence → Integrations**, **Add integration** and choose the provider:

| Provider | Signs in with | Used for |
| --- | --- | --- |
| **Google** | An OAuth app you register in Google Cloud (client id and secret) | Google Drive file sources |
| **Microsoft 365** | An app you register in Microsoft Entra (client id and secret, and your tenant) | SharePoint and OneDrive file sources |
| **HubSpot** | A private app token with the contacts and calls scopes | Looking customers up, logging calls |
| **Salesforce** | A connected app (client id and secret) | Looking customers up, logging calls |
| **Notion** | An internal integration secret | Notion file sources |

For Google, Microsoft 365 and Salesforce, give the app the redirect address the editor shows, then save: you are sent to the provider to approve it, and the connection keeps a refresh token, encrypted, so it stays signed in. **Reconnect** signs it in again. HubSpot and Notion take the token you paste in.

## Appointments

AI Integrations does not book appointments. A booking service with an API is connected with workflows: a **Workflow** tool instance whose workflow calls the service's API with **HTTP Request** (see [Automate AI conversations](conversation-workflows.md)).

## CRMs

With a HubSpot or Salesforce connection:

- **Look a caller up**: choose the **CRM** on the profile's **Capabilities** tab and add the **Find CRM Contact** tool. It finds the customer by the number they call from, or one the AI gives it.
- **Log the conversation**: add the **Log to CRM** task to an **AI Chat Session Closed** or **AI Chat Session Post-Processed** workflow. It finds the customer by phone (adding them when missing, unless you untick it), writes the fields you list as `field: value` lines in Liquid (`firstname`, `lastname` and `email` are the contact's own fields; any other name is the CRM's own field, such as `lifecyclestage` in HubSpot or `LeadSource` in Salesforce), and logs a completed call with the title and notes you give, such as the summary and the outcome. Its outcomes are **Done**, **Not found** and **Failed**, and `CrmContactId` is in its output.
- **Start an AI call from the CRM**: the CRM's own automation calls the [AI API](../../ai/conversations-api.md) (`POST /api/ai/v1/calls`) with an API key, or an **HTTP Request Event** workflow that runs **Create AI Activity**.

Other CRMs are connected the same way as other booking services, with workflows and **HTTP Request**.
