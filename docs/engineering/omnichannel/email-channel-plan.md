---
sidebar_label: "Email Channel Plan"
title: Email Omnichannel Channel — Project Plan
description: Add email as a channel of the Omnichannel Messaging workspace and of Omnichannel automation, with provider-agnostic sending, webhook and mailbox-polling inbound, entry-point routing to people, queues or an AI agent, and email campaigns.
---

# Email Omnichannel Channel — Project Plan

## Why

The messaging workspace was built to be one inbox for every channel, but SMS is the only channel today. Email is the next channel. It must work the way SMS works:

- An agent writes a message in the workspace and it leaves as an email.
- A customer's email arrives in the workspace, on the right conversation, routed by the address's entry point to an agent, a queue or an AI agent.
- Activities can be loaded on the Email channel, manual or automated, and an automated (AI) email conversation hands off to a person or a queue the way an SMS one does.
- Campaigns run on email: the AI sends the opening email, answers replies, follows up on a cadence and concludes with a disposition.

Email providers differ more than SMS providers do. Some post inbound mail to a webhook (SendGrid Inbound Parse, Mailgun Routes, Postmark, Amazon SES through SNS, Cloudflare Email Workers). Many cannot, and only offer a mailbox (Microsoft 365, Google Workspace, any IMAP host). Sending is just as varied. The channel therefore has a provider seam on both sides.

## What already exists

| Piece | Where |
| --- | --- |
| `OmnichannelConstants.Channels.Email`, `ActivityKind.Email`, `InteractionChannel.Email` | Omnichannel.Core, ContactCenter.Abstractions |
| `OmnichannelAddressTypes.EmailAddress` (registered, hidden until a feature adds an Email capability) | Omnichannel.Managements |
| Contact emails (`ContactMethods` bag, `EmailInfoPart`), `OmnichannelContactPart.DoNotEmail` | Omnichannel.Core |
| `OmnichannelHelper.FindDestination` for Email, `IncludeDoNoEmail` on loads | Omnichannel.Managements |
| `MessagingChannelCapabilities.SupportsSubject`, `MessagingOutboundMessage.Subject`, composer subject input | Messaging.Core, Messaging module |
| `MessagingAgentHandoffService` accepts every registered messaging channel | Messaging.Core |
| Durable provider webhook inbox | ContactCenter.ProviderInbox |

## Gaps found

1. `OmnichannelMessage` has no subject, so a subject is sent once and lost: the outbox retry, the thread and the auto-reply never see it.
2. `MessagingOutboundMessage` carries no conversation, so a channel cannot thread a reply (`In-Reply-To`, `Re:` subject). It carries attachments only as public links, which email does not need.
3. Email addresses do not match case-insensitively: the address editor only trims them, the store compares exactly, and the contact index stores the primary email as typed.
4. Load Activities, subject flows and the report filter hard-code Phone and SMS (`IsKnownChannel` rejects anything else).
5. The default "From" address in the composer is SMS-only.
6. The automated (AI) conversation, its opening message, the entry-point AI starter, the owed-reply recovery and the cadence follow-ups are written for SMS only.
7. The composer sends on Enter and is two rows high; bubbles do not show a subject or fold quoted text.

## Design

### Projects

| Project | Kind | Contents |
| --- | --- | --- |
| `CrestApps.OrchardCore.Omnichannel.Messaging.Email.Core` | library | `EmailMessagingChannel`, address normalization, transports, inbound parsing (MIME, provider webhooks), reply/quote stripping, HTML to text, the inbound receiver, the inbox handler, the mailbox (IMAP) reader, unsubscribe tokens. |
| `CrestApps.OrchardCore.Omnichannel.Messaging.Email` | module | Feature **Email Messaging Channel**: registrations, the address editor card, webhook and unsubscribe endpoints, the mailbox polling task, settings. |
| `CrestApps.OrchardCore.Omnichannel.Automation.Core` | library | The channel-neutral automated conversation engine: reply loop, should-respond gate, conclusion, handoff, opening message, entry-point AI starter, owed-reply recovery, cadence follow-ups. A channel plugs in through `IAutomatedMessagingChannel`. |
| `CrestApps.OrchardCore.Omnichannel.Email` | module | Feature **Email Omnichannel Automation**: the email adapter for the engine, the AI templates, the entry-point AI agent card. |

The engine is additive shared infrastructure. Email uses it first. SMS automation keeps its own handler in this change and moves onto the engine in a later, separately tested step.

### Sending (provider-agnostic)

`IEmailTransport` is the outbound seam, picked per address:

| Transport | Use |
| --- | --- |
| `OrchardCore` (default) | Orchard Core's email service, with the tenant's default provider or a named one (SMTP, Azure Communication Services, any provider module). Cannot set threading headers. |
| `Smtp` | The address's own mailbox server through MailKit. Sets `Message-ID`, `In-Reply-To`, `References`, `Auto-Submitted` and `List-Unsubscribe`, so replies thread in every mail client. |

More transports (a provider HTTP API) register with `services.AddEmailTransport<T>()`.

The channel builds a plain-text and an HTML body, appends the address's signature, attaches files from the attachment store, and threads the reply: the subject defaults to `Re:` and the subject of the customer's last email, and `In-Reply-To` and `References` point at it.

### Receiving (every kind of provider)

Every source produces a normalized `InboundEmail` and hands it to `IEmailInboundReceiver`:

| Source | How |
| --- | --- |
| Webhook | `POST api/omnichannel/email/inbound/{provider}?key=…` with parsers for `sendgrid`, `mailgun` (signature checked), `postmark`, `ses` (SNS signature checked, subscription confirmed), `mime` (raw RFC 822, for Cloudflare Email Workers, Lambda and the like) and `json` (a documented schema, for Power Automate, Zapier, Make, Google Apps Script). |
| Mailbox | An IMAP poller per address (Microsoft 365, Google Workspace, any IMAP host), tracking `UIDVALIDITY`/UID, then marking messages read or moving them to a folder. |

The receiver:

1. Drops mail from our own addresses (loops).
2. Turns a bounce (delivery status notification) into a failed delivery receipt on the original message.
3. Finds our address among the envelope, `To`, `Cc`, `Delivered-To` and `X-Original-To` recipients.
4. Stores the attachments in the encrypted attachment store, skipping inline images the HTML references.
5. Strips the quoted history from the reply and keeps it as folded quoted text.
6. Flags automatic mail (`Auto-Submitted`, `Precedence: bulk`, out-of-office headers) so neither the auto-reply nor the AI answers it.
7. Commits the message to the durable provider inbox, keyed on its `Message-ID`, and dispatches it.

The inbox handler raises `EmailReceived` on the Omnichannel event bus. The workspace's handler feeds it to `IMessagingInboundProcessor`, and the automation handler answers it when an automated activity owns the contact. Entry points route the conversation exactly as for SMS: agent or queue, shared pool or routed, auto-reply, or an AI agent.

### Workspace changes (channel-neutral)

- `OmnichannelMessage.Subject`, stored on every outbound and inbound message, shown on the bubble, resent by the outbox.
- `MessagingOutboundMessage.ConversationId`, `Attachments` and `Purpose` (reply, auto-reply, broadcast, automation).
- `MessagingAttachmentCapabilities.DeliveredAsLinks`: a channel that embeds files (email) does not need a public site URL.
- `MessagingQuotedText` on a message, rendered folded.
- Subject on the new-message composer and on broadcasts, shown when the channel supports subjects; the reply composer suggests `Re:`.
- Channels with subjects get a taller composer where Enter adds a line and Ctrl+Enter sends.
- The default "From" address is the agent's messaging line on any channel.
- A provider-neutral `recipient_rejected` refusal is not retried.

### Omnichannel changes

- Email addresses are stored and matched in lower case: the address editor, the address store, and a new `NormalizedPrimaryEmailAddress` contact index column (backfilled by a migration).
- Load Activities, subject flows and the report filter list the channels in `ActivityChannelOptions` instead of a fixed list. The Email channel feature adds Email.
- Email activities store their destination in lower case so a reply matches its activity.

### Automation (AI)

- Opening email: the profile's opening message is rendered as for SMS; a first line `Subject: …` becomes the subject, otherwise the address's default subject is used.
- Replies: no typing delay; a short settle so several emails sent together get one answer; the should-respond gate, conclusion, dispositions, lead conversion and handoff are the shared ones.
- Opt-out: a reply that only asks to unsubscribe, or the unsubscribe link, sets **Do not email** and cancels the activity.
- Campaign and cadence emails carry an unsubscribe link and the one-click `List-Unsubscribe` header (SMTP transport).

## Phases

1. Workspace neutrality: subject, conversation id, attachments, quoted text, composer, default address, retry codes.
2. Email address normalization and contact index.
3. Email channel core and feature: channel, transports, address editor, settings.
4. Inbound: receiver, parsers, webhooks, inbox handler, mailbox poller, bounces, auto-reply detection.
5. Activities: channel lists from options, Load Activities on Email.
6. Automation engine and the Email Omnichannel Automation feature.
7. Unsubscribe endpoint and campaign compliance.
8. Tests, docs, changelog, feature reference, skill update.

## Open follow-ups

- Move SMS automation onto the shared engine.
- OAuth (XOAUTH2) mailbox sign-in for Microsoft 365 and Google, and Microsoft Graph / Gmail push subscriptions as further inbound sources.
- Rich (HTML) composing in the workspace.
