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
- Bounce handling: a delivery report about an email the site sent marks that email as failed, and a hard bounce suppresses the address.
- Sending limits, warm-up, pacing by receiving domain, a suppression list, bounce and complaint webhooks, and pauses when servers push back or sending health turns bad (see [Send email at volume](#send-email-at-volume)).
- Loop protection: automatic mail (out-of-office replies, bulk mail, bounces) is recorded but never auto-replied to, and mail from one of the business's own addresses is ignored.
- One-click unsubscribe on broadcasts and campaign email, which opts the address out of email, whoever holds it, and marks every contact holding it **Do not email**.
- A personal-data export or erasure covers the person's emails, attachments, delivery log and suppression note (see [Personal data](#personal-data)).
- Email on Load Activities, subject flows and the activity report filters.

**Email Omnichannel Automation**

- The AI sends an automated activity's opening email, answers the customer's replies in the same thread, judges when a reply is warranted, concludes with a disposition, and hands the customer to a person or a queue.
- An **AI agent** routing choice on email entry points, so an AI profile answers the first email a customer sends to an address.
- Follow-up emails on the campaign's [cadence](cadences.md), within business hours.
- Recovery of a reply lost to a restart (only for a customer email from the last 30 minutes).
- **Customer care by email** and **Qualify leads by email** AI profile starting points, and an **Email Writing Style** prompt.

## Enable the features

1. In the admin, go to **Tools** → **Features**.
2. Enable **Email Messaging Channel**. It brings in the Omnichannel Messaging workspace, the Contact Center provider inbox and Orchard Core's Email feature.
3. To let the AI work email, enable **Email Omnichannel Automation**. It brings in the AI features, Omnichannel Management and Contact Center Business Hours.

## Add an email address

1. Go to **Interaction Center** → **Management** → **Omnichannel Addresses** and add an address of type **Email address**, such as `support@contoso.com`. Tick **Email**.
2. On the **Email** card, set how it sends:
   - **Sender name**, shown to the customer, and a **Signature** added below every email.
   - **Send through**:
     - **Orchard Core email service** uses the site's email settings (**Settings** → **Communication** → **Email**). Pick a provider, or leave it on the site's default. The provider must be allowed to send from this address (for SMTP, the mailbox's credentials; for Azure Communication Services, a verified sender domain). It cannot add the one-click unsubscribe header (`List-Unsubscribe`) that Gmail and Yahoo require of bulk senders, and the card warns about it, so an address that sends broadcasts or campaigns should use its own SMTP server.
     - **The address's own SMTP server** sends from the mailbox itself, for example `smtp.office365.com` on port 587 with STARTTLS, or `smtp.gmail.com`. Use **Test Connection** to check the sign-in before saving. This transport sets the threading headers, so replies stay threaded in every mail client, and the one-click unsubscribe header on bulk mail. A sending service's SMTP relay (Amazon SES, SendGrid, Mailgun, Postmark) works here too.
   - The SMTP and IMAP passwords are stored and never shown again; leave the box empty to keep the stored one. The stored password is used (by **Test Connection**, on save, and when the site connects) only while the host, port, security and user name are the ones it was saved with. It is stored sealed to them, so a password copied next to other settings (through the API, a recipe or an import) is never sent. Change any of them and type the password again. With **Auto** security, a server that wants a sign-in but offers no encryption gets no password: choose STARTTLS or SSL/TLS, or **None** to send it unencrypted on purpose.
   - **Default subject**, used for a new email that is not a reply and was given none.
3. Set how mail reaches the workspace in **Receive mail by** (see below), and save.
4. Add the address to an **email entry point** (**Interaction Center** → **Management** → **Inbound Entry Points**, then **Create** → **Email**) to say who answers it: a queue, an agent or an AI agent.
5. Optionally list the agents who send from the address on the same card, so the composer starts their new email conversations from it.

Addresses are stored and matched in lower case, so mail to `Support@Contoso.com` reaches `support@contoso.com`.

## Receive mail

### Webhook

Choose **My email provider posts it to a webhook**. The webhook addresses are on the address's Email card and on **Settings** → **Communication** → **Email**, under **Inbound Email**. Save that settings page once to create the site's webhook key; every webhook address carries it, so keep the addresses private, and use **Create a new webhook key** if one leaks.

The route is `POST ~/api/omnichannel/email/inbound/{provider}?key=…`. The key can also be sent in an `X-Webhook-Key` header.

| Provider | `{provider}` | Set up in the provider |
| --- | --- | --- |
| SendGrid | `sendgrid` | **Settings** → **Inbound Parse**: add the receiving domain and the webhook address. Ticking **POST the raw, full MIME message** is recommended. |
| Mailgun | `mailgun` | **Receiving** → **Routes**: a route whose action is `forward("…webhook address…")`. The parsed fields are read, and the raw message (`body-mime`) when the forwarding address ends in `mime`. Paste the **HTTP webhook signing key** in the site's inbound email settings so every call's signature is checked; until it is set, every unsigned call is logged as a warning. |
| Postmark | `postmark` | The inbound stream's **Webhook** setting. |
| Amazon SES | `ses` | A receipt rule with an **SNS** action whose **encoding** is UTF-8 or Base64, and an HTTPS subscription of that topic to the webhook address. The subscription is confirmed automatically. List the topic ARN under **Allowed Amazon SNS topics** to refuse any other topic; until the list is set, every call is logged as a warning. |
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

Choose **Reading the mailbox (IMAP)** when the provider cannot post inbound mail. Enter the IMAP server (for example `outlook.office365.com` or `imap.gmail.com`, port 993 with SSL/TLS), the user name and the password, and use **Test Connection**. For Google, use an app password; for Microsoft 365, the tenant must allow IMAP sign-in for the mailbox.

The **Email Mailbox Reader** background task reads each mailbox every minute, receives the mail that arrived since the last read, then marks it as read or moves it to a folder. The first read receives only mail that arrives after the mailbox is connected, unless **On first connect** asks for the unread mail of the last few days. A mailbox that fails (a changed password, a server that is down) is retried less and less often, up to once an hour, and the error shows on the address's Email card.

An email over 30 MB is not downloaded, and an email that cannot be read is passed over: both are left unread where they are, for a person to open in their mail client, and the log says why. An email the site fails to receive (the database or the file store briefly out of reach) is tried again by the next reads, up to five times, before it is passed over the same way. The next email is received as usual meanwhile, so one bad email never holds up the mailbox. A dropped connection ends the read instead, and the next read starts again from the same email.

### What happens to an inbound email

1. Mail from one of the business's own addresses is ignored, so two mailboxes never answer each other, unless its `Reply-To` names somebody else (a form on the business's own site relaying a visitor's message; see below).
2. A delivery report (a bounce) is never received as a message. Anybody can email one, so it is believed only when it is about an email this site sent to the recipient it names, and any other report is ignored. The email is found by its `Message-ID` when the transport set one (the SMTP transport does, and so do the AI's emails sent through it). Orchard Core's email service cannot set the `Message-ID`, so a report about an email sent through it is tied to the newest email sent to that recipient in the last seven days, and the reports about one email count as one bounce however many arrive. Anyone who knows a recipient the site mailed lately can write such a report, so it only counts toward the address's health: a hard bounce counts as a soft bounce and a block as a deferral, and it never suppresses the recipient, fails an email or pauses the address. Use the address's own SMTP server or a provider's bounce webhook for bounces that suppress. A failure (`Action: failed`) or a delay (`delayed`) is then read as a provider's bounce report is (see [What the channel does](#what-the-channel-does)): a hard bounce marks the email failed and suppresses the recipient. Success notices (`delivered`, `relayed`, `expanded`) and read receipts are ignored.
3. The address is found among the delivery headers (`Delivered-To`, `X-Original-To`, the envelope), `To` and `Cc`, so a Bcc or a forwarding alias still reaches its address.
4. The files are kept in the workspace's encrypted attachment store. A picture the HTML body shows inline (a logo in a signature) is left out.
5. The new reply is separated from the quoted history (the "On … wrote:" and Outlook header blocks of the common mail clients, and lines quoted with `>`).
6. An automatic email (`Auto-Submitted`, `Precedence: bulk`, out-of-office headers) is flagged, so the entry point's auto-reply and the AI leave it unanswered.
7. The email is committed to the durable inbox under its `Message-ID` and handed to the workspace and, when an automated conversation owns the customer, to the AI.

The conversation is with the address the email came from. A `Reply-To` naming somebody else decides the conversation only when one of the business's own addresses sent the email, as a form on the business's own site relays a visitor's message; from anyone else it is kept on the message for the record, because anybody can write any `Reply-To`.

A `From` line is just as easy to forge, so mail "from" one of the business's own addresses is believed only when the system that received it reports the business's domain authenticated: DMARC passed, or a DKIM signature by that domain did. Otherwise anybody could make the workspace, or the AI, write to a stranger by sending mail from the business with that stranger as the `Reply-To`. Unauthenticated mail from the business's own addresses is dropped as the business's own mail coming back. The report is read from where the sender cannot write it:

| How the email arrives | What is read |
|---|---|
| Reading the mailbox (IMAP) | The topmost `Authentication-Results` header, which a mailbox host that checks DMARC adds above the sender's headers (Microsoft 365 and Google Workspace do). The server that wrote it is not checked, so on a host that adds none, the sender's own header is read. |
| Amazon SES | The `dmarcVerdict` in the signed SNS notification, taken only when the From domain SES read is the one the site read. |
| SendGrid | Its DKIM check (`dkim`): a passing signature by the business's domain. |
| Mailgun, Postmark, raw MIME, JSON | Nothing these webhooks post is a check the provider made, so a form's relayed message is not taken from them. Send the form through a mailbox read over IMAP, Amazon SES or SendGrid. |

For the domain to pass, the website form must send from the business's domain with DKIM signing (or an SPF record for the server it sends from) and a DMARC record for the domain.

The customer is matched to the contact or lead that holds the address among any of its email addresses, not only the first. On an upgraded site, the contacts and leads saved before this was added are indexed in the background by the **Resumable upgrade steps** task, up to 2,000 a minute.

## Answer email in the workspace

Email conversations show under the envelope tab of a customer. Each message shows its subject, and **Show quoted text** opens the history it quoted. The reply composer suggests `Re:` and the latest subject, is taller than the SMS one, and keeps Enter for new lines: press **Ctrl+Enter** to send. Files of any document or picture format can be attached, up to ten and 20 MB per email.

**New message** shows a **Subject** line when the address picked is an email address, and so do broadcasts. A broadcast email carries the unsubscribe link unless the address turns it off, and the one-click unsubscribe header when the address sends through its own SMTP server.

A broadcast opens no conversation for each recipient. Each recipient gets the email on their own, and a conversation opens when one of them replies, with the broadcast in it. A recipient who already has a conversation gets the email in it, without it being reopened, marked read or given to someone else. An email refused for good (a suppressed or opted-out address) to someone who has no conversation leaves nothing behind. An email the address's sending limits hold back is counted on the broadcast list as queued, apart from the sent ones, and the broadcast shows **Sending** until the queued emails have gone out.

## Automate email (AI)

With **Email Omnichannel Automation** on:

- **Load Activities** offers the **Email** channel. An automated (**Automatic** source) load sends each contact an opening email from the address picked on the load, or from the subject flow's address. The contact's destination is their first valid email address, in lower case.
- The opening email is the AI profile's **Start the conversation automatically** message. When its first line is `Subject: …`, that line becomes the email's subject; otherwise the address's default subject is used. The email starting points ship with such a first line.
- The AI answers each reply in the same thread, after a short settle (15 seconds, or the load's reply delay, at most five minutes) that gathers several emails sent together into one answer. It has no typing pause.
- A reply that only asks to unsubscribe ("unsubscribe", "remove me", "stop") marks the contact **Do not email** and cancels the activity.
- Cadence follow-ups reply under the opening email's subject, within business hours, and carry the unsubscribe link.
- A handoff copies the transcript and a short summary to a workspace conversation on the target queue, as for SMS.
- On an email entry point, choosing **AI agent** under **Route to** lets an AI profile answer a customer's first email. The same rules as for texts decide when a person takes the message instead: the entry point is closed, a person already has an open conversation with the customer, or the customer's last AI conversation ended less than an hour ago.

Anybody can write to an address, so automated replies have safeguards:

- The subject of the customer's email reaches the AI inside the customer's own message, on one line of at most 200 characters, never as an instruction.
- The AI profile's own tools are not offered on an automated email reply. The only tool is the transfer to a person, when one is available.
- A message that reaches the AI twice (a provider's redelivery under the same `Message-ID`) is answered once.
- The AI sends at most five messages in a conversation within an hour, its opening email and follow-ups included. Past that it stops replying and hands the conversation to a person when one is available, so two automated mailboxes never write to each other forever.
- When the check of whether a reply is warranted cannot decide (no chat deployment, a failed call), the AI sends nothing. It hands the conversation to a person when one is available; otherwise the message stays owed, and the lost-reply recovery may try again while it is recent.
- Automated replies count against the address's hourly and daily limits and wait while its bulk mail is paused (see [Send email at volume](#send-email-at-volume)).

The AI's requests are recorded under the **Email** category of the AI usage report.

## Send email at volume

Mailbox providers (Gmail, Outlook, Yahoo) judge a sender by its sending history: how much it sends and how steadily, how many of its emails bounce, and how many people report them as spam. A campaign that ignores that is throttled, then sent to spam, then blocked, and the block lands on the address and the domain, not just the campaign. The channel does the parts that live in the platform; the rest is set up at your domain and your sending provider.

### What the channel does

- **Sending limits.** Each email address has a limit per hour, per day and per receiving domain, and a minimum gap between bulk emails, on the address's **Email** card. Broadcasts, campaign opening emails and follow-ups go out as fast as the minimum gap allows until a limit is reached; then they wait for their turns instead of failing, spread out at the address's pace (two hundred an hour is one every 18 seconds), so a campaign of ten thousand drains over hours rather than bursting. Automated replies (the AI or a workflow answering in a conversation) wait for room under the hourly and daily limits and for the address's pauses, but not for the gap, the per-domain limit or bulk mail's turns. An entry point's acknowledgement ("we got your message") that is still waiting when the conversation gets an answer is not sent, so the contact never hears a reply is coming after it came. A person's own replies count against the limits but are never held back. Saving new limits on the address, or resuming its paused bulk sending, gives the waiting mail new turns under the limits now in force, so raising a limit that was too low releases the mail right away instead of after the old turns.
- **Warm-up.** **Warm up this address** starts a new address at a small daily limit (50 by default) and doubles it every day until it reaches the daily limit, spreading each day's allowance over twelve hours. The warm-up counts from the day it was turned on; an address saved or imported with it on and no start date starts its warm-up that day.
- **Reacting to the server.** A failed send is read by its SMTP code and enhanced status code:

  | The server says | The channel |
  | --- | --- |
  | `5.1.x`, `5.2.1`, `5.4.x`, `5.0.0` (no such mailbox or domain, a disabled mailbox) | Suppresses the recipient and never retries it. A `5.0.0` or `5.4.x` whose words say the sender was refused (a block list, a policy) is a block, `5.1.7` and `5.1.8` (about the sender's own address) are blocks, `5.4.5` is a throttle and `5.4.7` (expired in the queue) is temporary. A `550` without an enhanced code suppresses only when its words, or a refused recipient, say the mailbox does not exist; otherwise it is a block. |
  | `5.3.x`, `5.5.x`, `5.6.x`, `5.2.3`, a `552` without an enhanced code (too large, refused content or format) | Only that email fails. The recipient is not suppressed and bulk mail is not paused, unless the words say the mailbox does not exist. |
  | `5.7.x` (policy, reputation, authentication) | Keeps the recipient, and pauses the address's bulk mail for an hour, then 2, 4… up to a day if it happens again after the pause ended. |
  | `4.7.x`, `4.2.1`, `4.4.5`, `5.4.5`, "rate limit", "too many", "try again later", a `421` without an enhanced code | Pauses bulk mail for 15 minutes, then 30, 60… up to 4 hours; held mail is sent when the pause ends. |
  | A full mailbox (`4.2.2`, `5.2.2`, or a `552` whose words say so) and other `4xx` | Retries later as an ordinary failure. A full mailbox reported with its code is about one recipient, so it never pauses the address. |

  The same push-back reported again while its pause runs (a batch of bounce reports, mail already on its way) belongs to that pause: it neither moves the end nor makes the next pause longer. Only push-back after a pause ended does, and a bulk email that gets through starts the backoff again. A pause never cuts a longer one short: a throttle during a day-long block leaves the block in place.

  Bounce reports, from a provider's webhook or back in a mailbox, are read by the same codes: a refusal of the sender pauses the address instead of suppressing the recipient, a refusal of the one email fails only that email, and a permanent full-mailbox answer (`5.2.2`) counts toward the recipient's soft bounces. A temporary failure (`4.x.x`) is never a hard bounce: one the provider is still retrying, such as each Mailgun temporary failure, is a deferral, and one a provider reports as a hard bounce counts as a soft bounce.

- **Suppression list.** A hard bounce, a third email to soft-bounce within two weeks, or a spam complaint puts the address on **Interaction Center** → **Management** → **Email Suppressions**, and nothing is sent to it again from any address. The list is also a source of consent, so every send path that asks whether an address may be emailed, broadcasts and campaigns among them, skips a suppressed address. Automated activities to a suppressed address are cancelled before anything is composed. An address can be taken off the list, or added by hand, on that page. The **Email Suppression List** deployment step exports the list, and the `EmailSuppressions` recipe step adds addresses to another site's list (an address already on it keeps its first reason):

  ```json
  {
    "name": "EmailSuppressions",
    "Suppressions": [
      { "Address": "ann@example.com", "Reason": "HardBounce", "Detail": "5.1.1 User unknown" }
    ]
  }
  ```

  `Reason` is `HardBounce`, `RepeatedSoftBounces`, `Complaint` or `Manual` (the default).

- **Bounce and complaint webhooks.** A provider that reports what happened after the email left posts it to `POST ~/api/omnichannel/email/events/{provider}?key=…` (`sendgrid`, `mailgun`, `postmark`, `ses`, `json`), listed under **Settings** → **Communication** → **Email**, **Inbound Email**. Point SendGrid's Event Webhook, Mailgun's webhooks, Postmark's bounce and spam complaint webhooks, or an Amazon SES bounce and complaint SNS topic at it. A complaint also opts the address out of email and marks every contact holding it **Do not email**. Bounce reports that come back to a mailbox the channel reads are handled the same way, once they are checked against the email they name (see [What happens to an inbound email](#what-happens-to-an-inbound-email)).
- **Receiving domains that push back.** When one receiving domain defers or soft-bounces three emails within half an hour, bulk mail to that domain waits half an hour while mail to every other domain goes on.
- **Sending health.** The **Email** card shows the last seven days: emails sent, the bounce and complaint rates, and where the address stands against its limits. When 5% of a week's emails bounce, or 0.3% are reported as spam (Gmail asks bulk senders to stay under 0.1%), bulk sending pauses until someone ticks **Resume bulk sending** after cleaning the list. Health is then measured from the resume, so the bounces and complaints before it do not pause the address again. Automated replies wait while bulk mail is paused; a person's replies are never paused. The delivery log these figures are counted from keeps 30 days; the **Email Delivery Log Pruning** background task removes older entries every ten minutes.
- **Unsubscribe.** Broadcasts and campaign email carry an unsubscribe link, and the one-click unsubscribe header (`List-Unsubscribe`) when the address sends through its own SMTP server (see [Answer email in the workspace](#answer-email-in-the-workspace)). An unsubscribe link works for 400 days, like a campaign's. Unsubscribing opts the address itself out of email, whoever holds it (a contact's second address, or an address no contact holds), and marks every contact holding it, among any of its email addresses, **Do not email**.

:::note[Throughput]
Limits apply to campaign and broadcast mail, and the hourly and daily limits to automated replies. A campaign larger than the address's daily limit takes more than a day; that is the point. Raise the limits gradually as the address builds a history, and only on a dedicated sending service.
:::

### What you set up outside the platform

- **Authenticate the sending domain.** Publish SPF, sign with DKIM, and publish a DMARC policy, with the DKIM domain aligned with the From address. Gmail and Yahoo refuse bulk mail without them. Your sending provider gives the records.
- **Use a sending service for campaigns.** A mailbox host limits one mailbox: Microsoft 365 takes about 30 messages a minute and 10,000 recipients a day, Google Workspace about 2,000 a day. For campaigns, use a dedicated sending service (Amazon SES, SendGrid, Mailgun, Postmark, Azure Communication Services) through its SMTP relay, set as the address's own SMTP server so campaign mail carries the one-click unsubscribe header, and connect its bounce and complaint webhook.
- **Keep campaign mail apart.** Send campaigns from their own address on their own subdomain (for example `news.contoso.com`), so a campaign that goes badly cannot hurt the address customers reply to or the mail your business depends on. Each address has its own limits, health and pause.
- **Warm up gradually, and start with your most engaged contacts.** Recipients who open and reply build the history; old or bought lists tear it down.
- **Do not rotate addresses or domains to get around a block.** Providers recognise the pattern and the content, and rotating looks more like spam, not less.

The channel does not measure inbox placement (accepted by the server is not the same as delivered to the inbox), monitor blocklists, or check the domain's DNS records; use your sending service's tools and Google Postmaster Tools for those.

## Personal data

The channel takes part in personal-data requests (see [Personal data](../modules/personal-data.md)), and finds what it keeps about a person by their email addresses:

- **The emails exchanged with them**, with their attachments. Erasing keeps each email as an empty record, so a conversation's message count and response times still add up, but clears its body, subject and quoted history, the sender's name, the addresses copied and the address it was exchanged with, and deletes its files from the attachment store.
- **The delivery log** of the mail sent to them. Erasing keeps the sending address, kind, time and receiving domain, which limits and health are counted from, and removes the recipient and what the server said.
- **Their suppression.** It stays, and only its note (what the server or provider said) is cleared, so a complaint or a hard bounce is never mailed again.
- **The AI conversations** an automated activity held with them, exported with the activity and deleted when it is erased (by the Omnichannel Activities contributor).

## Extend the channel

- **Another sending provider** (an HTTP API): implement `IEmailTransport` and register it with `services.AddEmailTransport<TTransport>()`. It appears in **Send through**.
- **Another inbound webhook format**: implement `IInboundEmailWebhookParser` and register it with `services.AddInboundEmailWebhookParser<TParser>()`. It is served at `api/omnichannel/email/inbound/{name}`.
- **Another bounce and complaint format**: implement `IEmailDeliveryEventParser` and register it with `services.AddEmailDeliveryEventParser<TParser>()`. It is served at `api/omnichannel/email/events/{name}`.
- **Another inbound source** (a provider's push API, a Microsoft Graph subscription): build an `InboundEmail` and call `IEmailInboundReceiver.ReceiveAsync`. The receiver does the rest: address matching, files, quote stripping, loop and bounce handling, and the durable inbox.
- **Another automated channel**: implement `IAutomatedMessagingChannel` and register it with `services.AddAutomatedMessagingChannel<TChannel>()`. The engine in `CrestApps.OrchardCore.Omnichannel.Automation.Core` provides the opening message, the reply loop, the conclusion, the handoff, the entry-point starter, the owed-reply recovery and the cadence follow-ups.
