---
sidebar_label: Email
sidebar_position: 4
title: Email Channel and Email Automation
description: Send and receive email in the Omnichannel Messaging workspace, route inbound email to agents, queues or an AI agent, and run automated email campaigns, with any email provider.
---

| | |
| --- | --- |
| **Feature Name** | Email Messaging Channel |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.Messaging.Email` |
| **Feature Name** | Email Omnichannel Automation |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.Email` |

Email is a channel of the [Omnichannel Messaging workspace](messaging-workspace.md), beside SMS. A customer's email arrives on their conversation, an agent answers it in the workspace and the answer leaves as an email in the same thread, and an email entry point decides who answers each address: a queue, an agent or an AI agent. With **Email Omnichannel Automation** on, activities can be loaded on the Email channel and worked by the AI, exactly as automated SMS activities are.

The channel works with any email provider. Each address picks how it sends, and how its mail comes in:

| | Options |
| --- | --- |
| **Sending** | Orchard Core's email service (SMTP, Azure Communication Services or any other Orchard Core email provider), or the address's own SMTP server. |
| **Receiving** | A webhook the provider posts each email to (SendGrid, Mailgun, Postmark, Amazon SES, raw MIME, JSON), or reading the address's mailbox over IMAP for providers that cannot post (Microsoft 365, Google Workspace, any IMAP host). |

## What the features provide

**Email Messaging Channel**

- Email addresses in **Omnichannel Addresses**, each with its sender name, signature, default subject, sending transport and receiving mode.
- Two-way email in the workspace: subjects, threaded replies (`Re:` and, with the SMTP transport, `In-Reply-To` and `References`), attachments of any document or picture format the workspace knows, and the history a reply quoted folded away under **Show quoted text**.
- Email **entry points** that route each address's mail with the workspace's distribution, opening hours and auto-replies.
- Inbound email from provider webhooks and from mailboxes, committed to the durable provider inbox and recognised by its `Message-ID`, so a redelivered email is never recorded or answered twice.
- Bounce handling: a delivery report marks the email it bounced as failed.
- Loop protection: automatic mail (out-of-office replies, bulk mail, bounces) is recorded but never auto-replied to, and mail from one of the business's own addresses is ignored.
- One-click unsubscribe on broadcasts and campaign email, which marks every contact holding the address **Do not email**.
- Email on Load Activities, subject flows and the activity report filters.

**Email Omnichannel Automation**

- The AI sends an automated activity's opening email, answers the customer's replies in the same thread, judges when a reply is warranted, concludes with a disposition, and hands the customer to a person or a queue.
- An **AI agent** routing choice on email entry points, so an AI profile answers the first email a customer sends to an address.
- Follow-up emails on the campaign's [cadence](cadences.md), within business hours.
- Recovery of a reply lost to a restart (only for a customer email from the last 30 minutes).
- **Customer care by email** and **Qualify leads by email** AI profile starting points, and an **Email Writing Style** prompt.

## Enable the features

1. In the admin, go to **Tools > Features**.
2. Enable **Email Messaging Channel**. It brings in the Omnichannel Messaging workspace, the Contact Center provider inbox and Orchard Core's Email feature.
3. To let the AI work email, enable **Email Omnichannel Automation**. It brings in the AI features, Omnichannel Management and Contact Center Business Hours.

## Add an email address

1. Go to **Omnichannel > Omnichannel Addresses** and add an address of type **Email address**, such as `support@contoso.com`. Tick **Email**.
2. On the **Email** card, set how it sends:
   - **Sender name**, shown to the customer, and a **Signature** added below every email.
   - **Send through**:
     - **Orchard Core email service** uses the site's email settings (**Settings > Communication > Email**). Pick a provider, or leave it on the site's default. The provider must be allowed to send from this address (for SMTP, the mailbox's credentials; for Azure Communication Services, a verified sender domain).
     - **The address's own SMTP server** sends from the mailbox itself, for example `smtp.office365.com` on port 587 with STARTTLS, or `smtp.gmail.com`. Use **Test connection** to check the sign-in before saving. This transport sets the threading headers, so replies stay threaded in every mail client.
   - **Default subject**, used for a new email that is not a reply and was given none.
3. Set how mail reaches the workspace in **Receive mail by** (see below), and save.
4. Add the address to an **email entry point** (**Contact Center > Entry Points**, then **Create > Email**) to say who answers it: a queue, an agent or an AI agent.
5. Optionally list the agents who send from the address on the same card, so the composer starts their new email conversations from it.

Addresses are stored and matched in lower case, so mail to `Support@Contoso.com` reaches `support@contoso.com`.

## Receive mail

### Webhook

Choose **My email provider posts it to a webhook**. The webhook addresses are on the address's Email card and on **Settings > Communication > Email**, under **Inbound email**. Save that settings page once to create the site's webhook key; every webhook address carries it, so keep the addresses private, and use **Create a new webhook key** if one leaks.

The route is `POST ~/api/omnichannel/email/inbound/{provider}?key=…`. The key can also be sent in an `X-Webhook-Key` header.

| Provider | `{provider}` | Set up in the provider |
| --- | --- | --- |
| SendGrid | `sendgrid` | **Settings > Inbound Parse**: add the receiving domain and the webhook address. Ticking **POST the raw, full MIME message** is recommended. |
| Mailgun | `mailgun` | **Receiving > Routes**: a route whose action is `forward("…webhook address…")`. The parsed fields are read, and the raw message (`body-mime`) when the forwarding address ends in `mime`. Paste the **HTTP webhook signing key** in the site's inbound email settings so every call's signature is checked. |
| Postmark | `postmark` | The inbound stream's **Webhook** setting. |
| Amazon SES | `ses` | A receipt rule with an **SNS** action whose **encoding** is UTF-8 or Base64, and an HTTPS subscription of that topic to the webhook address. The subscription is confirmed automatically. List the topic ARN under **Allowed Amazon SNS topics** to refuse any other topic. |
| Raw MIME | `mime` | Anything that can relay the raw RFC 822 message: a Cloudflare Email Worker, an AWS Lambda, a mail server's pipe-to-script. Post the message as the body, or as an `email` form field or file. Pass the envelope recipient as the `to` query value or the `X-Envelope-To` header when the headers may not name the address (a Bcc). |
| JSON | `json` | Automation platforms (Power Automate's "When a new email arrives" on Microsoft 365, Zapier, Make, n8n, a Google Apps Script on a Gmail inbox). See the shape below. |

The webhook answers as soon as the email is committed to the durable inbox, and processes it afterwards, so a provider never times out waiting for the workspace or the AI.

The JSON shape:

```json
{
  "from": "Ann Lee <ann@example.com>",
  "to": ["support@contoso.com"],
  "cc": [],
  "subject": "Order 1042",
  "text": "Hi, where is my order?",
  "html": "<p>Hi, where is my order?</p>",
  "messageId": "<CAF1@mail.example.com>",
  "inReplyTo": "<sent-1@contoso.com>",
  "references": ["<sent-1@contoso.com>"],
  "date": "2026-10-09T14:03:00Z",
  "headers": { "Auto-Submitted": "no" },
  "attachments": [ { "fileName": "invoice.pdf", "contentType": "application/pdf", "content": "<base64>" } ],
  "raw": "<optional: the whole RFC 822 message, as text or base64>"
}
```

When `raw` is present, the message is read from it. The body may also be an array of these objects.

### Mailbox (IMAP)

Choose **Reading the mailbox (IMAP)** when the provider cannot post inbound mail. Enter the IMAP server (for example `outlook.office365.com` or `imap.gmail.com`, port 993 with SSL/TLS), the user name and the password, and use **Test connection**. For Google, use an app password; for Microsoft 365, the tenant must allow IMAP sign-in for the mailbox.

The **Email Mailbox Reader** background task reads each mailbox every minute, receives the mail that arrived since the last read, then marks it as read or moves it to a folder. The first read receives only mail that arrives after the mailbox is connected, unless **On first connect** asks for the unread mail of the last few days. A mailbox that fails (a changed password, a server that is down) is retried less and less often, up to once an hour, and the error shows on the address's Email card.

### What happens to an inbound email

1. Mail from one of the business's own addresses is ignored, so two mailboxes never answer each other.
2. A bounce marks the email it bounced as failed (when the address sends with the SMTP transport, which sets the `Message-ID` the bounce names) and is not received as a message.
3. The address is found among the delivery headers (`Delivered-To`, `X-Original-To`, the envelope), `To` and `Cc`, so a Bcc or a forwarding alias still reaches its address.
4. The files are kept in the workspace's encrypted attachment store. A picture the HTML body shows inline (a logo in a signature) is left out.
5. The new reply is separated from the quoted history (the "On … wrote:" and Outlook header blocks of the common mail clients, and lines quoted with `>`).
6. An automatic email (`Auto-Submitted`, `Precedence: bulk`, out-of-office headers) is flagged, so the entry point's auto-reply and the AI leave it unanswered.
7. The email is committed to the durable inbox under its `Message-ID` and handed to the workspace and, when an automated conversation owns the customer, to the AI.

When the sender set a `Reply-To` (a web form or a notification service), the conversation is with that address.

## Answer email in the workspace

Email conversations show under the envelope tab of a customer. Each message shows its subject, and **Show quoted text** opens the history it quoted. The reply composer suggests `Re:` and the latest subject, is taller than the SMS one, and keeps Enter for new lines: press **Ctrl+Enter** to send. Files of any document or picture format can be attached, up to ten and 20 MB per email.

**New message** shows a **Subject** line when the address picked is an email address, and so do broadcasts. A broadcast email carries the one-click unsubscribe link and header unless the address turns them off.

## Automate email (AI)

With **Email Omnichannel Automation** on:

- **Load Activities** offers the **Email** channel. An automated (**Automatic** source) load sends each contact an opening email from the address picked on the load, or from the subject flow's address. The contact's destination is their first valid email address, in lower case.
- The opening email is the AI profile's **Start the conversation automatically** message. When its first line is `Subject: …`, that line becomes the email's subject; otherwise the address's default subject is used. The email starting points ship with such a first line.
- The AI answers each reply in the same thread, after a short settle (15 seconds, or the load's reply delay, at most five minutes) that gathers several emails sent together into one answer. It has no typing pause.
- A reply that only asks to unsubscribe ("unsubscribe", "remove me", "stop") marks the contact **Do not email** and cancels the activity.
- Cadence follow-ups reply under the opening email's subject, within business hours, and carry the unsubscribe link.
- A handoff copies the transcript and a short summary to a workspace conversation on the target queue, as for SMS.
- On an email entry point, **Route to > AI agent** lets an AI profile answer a customer's first email. The same rules as for texts decide when a person takes the message instead: the entry point is closed, a person already has an open conversation with the customer, or the customer's last AI conversation ended less than an hour ago.

The AI's requests are recorded under the **Email** category of the AI usage report.

## Extend the channel

- **Another sending provider** (an HTTP API): implement `IEmailTransport` and register it with `services.AddEmailTransport<TTransport>()`. It appears in **Send through**.
- **Another inbound webhook format**: implement `IInboundEmailWebhookParser` and register it with `services.AddInboundEmailWebhookParser<TParser>()`. It is served at `api/omnichannel/email/inbound/{name}`.
- **Another inbound source** (a provider's push API, a Microsoft Graph subscription): build an `InboundEmail` and call `IEmailInboundReceiver.ReceiveAsync`. The receiver does the rest: address matching, files, quote stripping, loop and bounce handling, and the durable inbox.
- **Another automated channel**: implement `IAutomatedMessagingChannel` and register it with `services.AddAutomatedMessagingChannel<TChannel>()`. The engine in `CrestApps.OrchardCore.Omnichannel.Automation.Core` provides the opening message, the reply loop, the conclusion, the handoff, the entry-point starter, the owed-reply recovery and the cadence follow-ups.
