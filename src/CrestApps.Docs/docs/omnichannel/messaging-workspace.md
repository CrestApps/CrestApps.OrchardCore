---
sidebar_label: Messaging Workspace
sidebar_position: 4
title: Omnichannel Messaging Workspace
description: A human-operated, channel-agnostic messaging inbox for Orchard Core. SMS is the first channel; email, WhatsApp, Messenger and others plug in as further channel features.
user_manual:
  - user-manual/messaging
  - user-manual/entry-points-and-ivr
  - user-manual/channel-endpoints
---

| | |
| --- | --- |
| **Feature Name** | Omnichannel Messaging Workspace |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.Messaging` |
| **Channel features** | `CrestApps.OrchardCore.Omnichannel.Messaging.Sms` (SMS Messaging Channel) |
| **Optional add-on** | `CrestApps.OrchardCore.Omnichannel.Messaging.RoutedDistribution` |
| **Category** | Contact Center |
| **Admin menu** | **Messaging** |

The **Messaging Workspace** is a **human-operated** inbox for every non-voice channel. Agents use one screen to send and receive on any channel for the same customer. Picture a list of customers on the left and the selected customer's conversation on the right, with a row of channel icons above it. Switching icons keeps the same view; it only changes which channel messages are read from and sent to.

The workspace itself carries **no channel**. Each channel is a separate feature that plugs into it:

| Channel | Feature | Status |
| --- | --- | --- |
| SMS | **SMS Messaging Channel** (`CrestApps.OrchardCore.Omnichannel.Messaging.Sms`) | Available |
| Email, WhatsApp, Facebook Messenger, Telegram, … | Future channel features | Build one with the channel contract below |

It is the human counterpart to [SMS Automation](sms.md), which lets an **AI agent** carry an SMS conversation on its own. Both can run on the same numbers. An automated activity handles a conversation until it hands off, and from then on the workspace owns the thread.

For the agent's step-by-step guide with screencasts (claiming, transferring, attachments, favorites, templates, broadcasts and the carrier keywords), see [Messaging](../user-manual/messaging.md) in the User Manual. This page covers how the workspace is built, set up, configured and extended.

## The workspace

The screens are described in the [User Manual](../user-manual/messaging.md). What matters behind them:

- **Customer-centric list.** The inbox lists customers, not conversations. A customer who wrote on several channels appears once, with the unread counts of all their channels summed and an icon per channel. The list filters by *All*, *Mine* or *Unassigned*, and by channel when more than one channel feature is enabled.
- **Channel tabs.** One tab per enabled channel above the open conversation. Switching tabs keeps the view and only changes which channel messages are read from and sent to. A tab's unread badge also counts the customer's new messages that arrive while the agent is scrolled up or on another browser tab, and clears once the agent is back at the bottom of the conversation. A channel where the customer has an address but no conversation opens the composer on that channel; a channel where the customer cannot be reached is disabled.
- **Attachments follow the channel.** The composer offers exactly the formats in the channel's `Capabilities.Attachments` and the server refuses anything else (see [Adding a channel](#adding-a-channel) and [Pictures (MMS)](#pictures-mms)).
- **Favorites** are stored per agent, up to 100 customers each (`MessagingFavorites.MaxFavorites`); starring a customer changes nothing for anyone else.
- **New message.** One recipient starts a conversation. Several recipients need `SendGroupMessages` and are queued as a group message, sent in the background as individual 1:1 conversations; a channel without broadcast support refuses more than one recipient.
- **Ownership.** Claim, transfer, close, spam and reopen behave the same on every channel. Replying to a conversation nobody holds claims it, under the same rules as **Claim**; replying never takes a conversation from the agent who holds it.
- **Transfer rules.** A transfer keeps the same conversation and history and does not notify the customer; the optional note (up to 500 characters) is recorded in the history only.
  - The holder may transfer it, and so may a supervisor with `ViewAllMessagingConversations`. A conversation nobody holds is claimed first; a supervisor can assign it directly.
  - **Back to a queue** is offered only when Contact Center Work Distribution is enabled.
  - Only users with `UseMessagingWorkspace` are offered as recipients, never the current holder and never the requester.
  - A queue's conversation handed to one of its members stays the queue's; handed to someone outside the queue, it becomes that person's own conversation, since they could not open it otherwise.
  - The recipient's list picks the conversation up at once and the sender's list drops it. The thread records *Transferred from A to B*.
- **Broadcasts.** One message to many recipients as individual 1:1 threads (not a group chat), on any channel whose capabilities support broadcasts. See [Broadcasts and templates](#broadcasts-and-templates).
- **Available toggle.** An agent with an agent profile has an **Available** switch above the customer list. When a queue's entry point uses **Routed** distribution, conversations are pushed only to agents who are available with their inbox open.
- **Real time.** New messages, delivery receipts and assignment changes are pushed over the workspace's own SignalR hub. A new message for the open conversation is appended, one for the same customer on another channel badges that channel's tab, and any new message moves its customer to the top of the list with its unread count. If a push is missed (a dropped connection), the open conversation catches up within seconds and the customer list within half a minute on its own.
- **Notifications on every admin page.** On any admin page other than the inbox, a notice appears when a customer writes on a conversation the user can see, when a conversation is transferred to them, and when one is sent back to a queue they serve.
- **Menu count.** **Messaging > Inbox** shows the unread open conversations assigned to the user, plus the unread open conversations nobody has taken yet in the queues they serve (in every queue, for a supervisor with `ViewAllMessagingConversations`). The parent **Messaging** item carries no number; its icon turns red while the count is above zero.

Only users with `UseMessagingWorkspace` get the notices and the count. Unless they can view all conversations, that starts once they have opened the workspace for the first time: that visit creates their agent profile, which is what ties them to their queues. Listening from another admin page never counts as being at the workspace: routed distribution only pushes conversations to agents whose **Inbox** is open, so an agent who is merely browsing other pages is not handed new work. The standalone soft phone page shows no notices.

Every notification the server sends is logged at the `Information` level (delivery receipts at `Debug`) with the event, the conversation and the groups it went to, so a missing notice can be traced. A notification with no one to go to, and a hub connection that can reach no group, are logged as warnings. A message on a conversation that no route assigned to an agent or queue goes only to users with `ViewAllMessagingConversations`; the log says so when that happens.

## Enable the features

1. Go to **Tools → Features**.
2. Enable **SMS Messaging Channel**. This also enables the **Omnichannel Messaging Workspace** and everything it needs (Channel Endpoints, Contact Center Agent Services, Contact Center Provider Webhook Inbox, SignalR, Orchard Core SMS).
3. Optionally enable **Omnichannel Messaging Routed Distribution** to push department conversations to the least-loaded available agent. This needs Contact Center Work Distribution.

Enabling the workspace on its own gives you the inbox with nothing to send or receive on. That is expected: channels come from channel features.

### Dependencies (and why they are minimal)

| Dependency | Why |
| --- | --- |
| **Omnichannel Channel Endpoints** | Every address you send from (an SMS number today; a mailbox or WhatsApp number later) is a [channel endpoint](management.md#omnichannel-address). It carries the provider. |
| **Contact Center Inbound Entry Points** | Where each number's messages go is set on the [inbound entry point](../user-manual/entry-points-and-ivr.md#text-entry-points) that answers it for the channel, with opening hours and auto-replies. Entry points route to a queue or an agent, so this brings Contact Center Work Distribution's queues and the agents with it. |
| **Contact Center Agent Services** (dependency-only) | Operators are Contact Center **agent profiles**. A bare profile is created automatically the first time a permitted user opens the workspace, so no Contact Center administration is required. |
| **Orchard Core SignalR** | The workspace's own real-time hub. |
| **Contact Center Provider Webhook Inbox** *(SMS channel only)* | The durable inbox that inbound provider deliveries are committed to before they are processed, so a retried delivery is de-duplicated and none is lost. |
| **Orchard Core SMS** *(SMS channel only)* | The SMS provider abstraction and the tenant default provider. |

The workspace does **not** require Contact Center Voice, and it does not pull in the Omnichannel Management CRM.

## Setting up SMS

1. **Configure an SMS provider.** Enable at least one, for example [Telnyx SMS](../telephony/telnyx.md#telnyx-sms) or Twilio, and pick the tenant **default provider** at **Settings > Communication > SMS**.
2. **Add your numbers as SMS channel endpoints** in **Interaction Center > Management > Omnichannel Addresses** (see [Omnichannel Addresses](../user-manual/channel-endpoints.md)). Each SMS endpoint can pin the **provider** that owns the number; leave it empty to use the tenant default.
3. **Add a Text messages entry point** for the numbers under **Interaction Center > Management > Inbound entry points**. It routes to an agent (personal number) or a queue (department number), chooses **Shared pool** or **Routed** queue distribution, and can send an auto-reply (at most once a day per conversation) and a closed auto-reply. The fields are described in [Text entry points](../user-manual/entry-points-and-ivr.md#text-entry-points).
4. **Grant the permissions** below to the roles that staff the inbox.
5. **Point the provider webhook at Orchard Core** so inbound messages and delivery receipts arrive. Telnyx SMS maps `api/telnyx/webhook/sms` (see the [Telnyx SMS webhook](../telephony/telnyx.md#telnyx-sms)). The Twilio inbound webhook, `api/twilio/webhook/sms`, is mapped by this channel whenever Orchard Core's Twilio SMS feature is on, so the workspace receives Twilio texts without the AI features. Its full address shows as **Webhook URL** on the Twilio tab of **Settings > Communication > SMS**.
6. Open **Messaging → Inbox**.

A **Send SMS** button appears beside phone-number fields on admin pages. It opens the customer's existing SMS conversation, or the composer when there is none.

### SMS compliance

The SMS channel applies the carrier keywords, whatever the workspace is doing:

| Keyword | Synonyms | What happens | Default reply |
| --- | --- | --- | --- |
| **STOP** | STOPALL, UNSUBSCRIBE, CANCEL, END, QUIT | Closes the conversation, sets the contact's **Do not SMS** flag and sends one confirmation. | *You have been unsubscribed and will not receive further messages. Reply START to resubscribe.* |
| **START** | UNSTOP, YES | Reverses an opt-out and confirms. It is ignored when the contact is not opted out, so an ordinary "Yes" stays part of the conversation. | *You have been resubscribed and will receive messages again. Reply STOP to unsubscribe.* |
| **HELP** | INFO | Answers with the help reply. | *Reply STOP to unsubscribe. Message and data rates may apply.* |

A keyword silences the entry point's auto-reply for that message. Replies can be customised under the `CrestApps:Omnichannel:Messaging:Sms:KeywordReplies` configuration section (`StopMessage`, `HelpMessage`, `StartMessage`).

SMS also observes **quiet hours**: outside the destination queue's business hours, in the contact's local time, the conversation shows a banner above the composer. The banner is a warning only; it never blocks sending. The `SendMessagesDuringQuietHours` permission only changes how the banner looks: without it, the banner adds that sending now may reach the contact at an unsociable hour.

### Outbound retries

An outbound message the provider does not accept is queued and retried after 1, 5, 15, and 60 minutes — five attempts in all — before it is marked failed. A refusal because the recipient opted out is never retried.

Some providers manage opt-outs themselves (Twilio's opt-out management on toll-free numbers and Messaging Services, Telnyx's STOP handling): they confirm the opt-out to the customer and refuse any further message to them, including the workspace's own STOP confirmation. That refusal is expected and is logged as information, not as a warning. Whenever a provider refuses a message because the recipient opted out, the contact is marked **Do not SMS** and the message is marked failed at once rather than retried.

### Pictures (MMS)

The SMS channel carries pictures in both directions: JPEG, PNG, GIF and WebP, up to 10 per message. A file is accepted as a picture only when its contents are one of those formats, whatever its name or the type the browser reports; anything else, including documents, is refused on SMS.

**Sending.** Carriers refuse picture messages much over 1 MB, so the composer shrinks larger photos to fit before sending: each picture gets its share of that budget, and a still picture is redrawn smaller as a JPEG until it fits. An animated GIF is never redrawn, because that would lose the animation, so a GIF over its share is refused. A picture message is sent through a provider that can carry pictures:

- **Telnyx** sends the pictures with the message.
- **Twilio** numbers send pictures through the workspace's own Twilio sender, which uses the account in **Settings > Communication > SMS** (OrchardCore's Twilio provider sends text only).
- Any other provider refuses a message with pictures rather than delivering the text alone, and the message is marked failed without retries.

The provider downloads each picture from the site, so the site must be reachable from the internet. The link is built from the **Base URL** in **Settings > General**, or from the address of the current request when no base URL is set. Messages retried later in the background, and messages sent from a site reached only by a local or internal address, need the base URL set to the public address. The links are signed, name nothing but the picture, and expire after `AttachmentLinkLifetimeHours` (72 by default); a retried message gets fresh ones. The information-level log line *Built a public picture link on (host)* shows which address the provider was given.

**Receiving.** When a customer sends a picture, the workspace copies it from the provider as the message arrives, because providers only keep it for a while. A picture over `MaxInboundAttachmentBytes` (10 MB by default), a file that is not a supported picture, or one the provider refuses to hand over is not kept; the thread then says an attachment could not be shown, and the text of the message is kept either way. Pictures sent to a Twilio number are downloaded with the tenant's Twilio credentials, so an account that requires authentication for media works as well. Each copied or skipped item is logged at the `Information` or `Warning` level with the provider's message id.

**Storage.** Pictures are kept in the tenant's own `App_Data` folder (`MessagingAttachments`), encrypted with the tenant's data protection keys, never in the public media library. In the workspace they are only shown to someone who may open the conversation they belong to.

## Hand-off from an automated conversation

While an automated (AI) activity is handling a contact on an endpoint, the workspace leaves that contact's messages to the automated agent, even when a workspace conversation for them already exists. When the automated agent hands off, the whole automated transcript is copied into the conversation, so the agent inherits every message, in order, each exactly once. Customer messages keep the provider's message id, which is how a message the thread already holds, or a provider's redelivery of one, is recognised and not recorded again. Threads written before this behaviour are shown with such duplicates collapsed. This works the same on every messaging channel.

## Permissions

| Permission | Display name | Grants |
| --- | --- | --- |
| `UseMessagingWorkspace` | Use the messaging workspace and view your own conversations | Open the workspace and work your own conversations: the ones assigned to you, and the ones sent to an endpoint you own that no colleague has claimed. |
| `ViewQueueMessagingConversations` | View unclaimed messaging conversations in your queues | Read, claim and answer the conversations nobody has claimed yet in the queues you serve, as far as your agent entitlements allow. |
| `ViewAllMessagingConversations` | View all messaging conversations | See every conversation (supervisors), including the ones colleagues have claimed and the ones no route gave to an agent or a queue, and transfer any of them. The holder of a conversation can transfer it without this. |
| `SendGroupMessages` | Send group messages | Send broadcasts and multi-recipient messages. |
| `SendMessagesDuringQuietHours` | Send messages outside business hours | Changes the quiet-hours banner to a plain notice without the unsociable-hour warning. Sending is never blocked, with or without it. |
| `ManageMessaging` | Manage the messaging workspace | Manage templates (**Messaging > Templates**). |

None of these implies another, except that `ViewAllMessagingConversations` includes the other two viewing permissions. An agent never sees a conversation a colleague has claimed. By default the **Agent** role gets `UseMessagingWorkspace` and `ViewQueueMessagingConversations`; the **Supervisor** role also gets `ViewAllMessagingConversations`, `SendGroupMessages`, `SendMessagesDuringQuietHours` and `ManageMessaging`. A role with `UseMessagingWorkspace` alone sees only its users' own conversations, which suits people who text from their own number and serve no queue.

Permissions apply to every channel; there is no per-channel permission. Where a number's messages go is edited on its entry point, under **Interaction Center > Management > Inbound entry points**, which requires the **Manage Contact Center queues** permission.

## Broadcasts and templates

**Messaging > Templates** (requires `ManageMessaging`) lists the canned responses agents insert from the composer. Each template has a **Name** and a **Body**.

**Messaging > Broadcasts** (requires `SendGroupMessages`) lists sent broadcasts with their channel, sending address, status, recipient, sent and failed counts. A new broadcast names a sending channel endpoint (which decides the channel), contacts reachable on that channel and any additional addresses, and the message. It is queued and sent in the background; each recipient gets their own 1:1 thread and cannot see the others, and a queued broadcast cannot be cancelled. The step-by-step guide is in [Templates](../user-manual/messaging.md#templates) and [Broadcasts](../user-manual/messaging.md#broadcasts) in the User Manual.

### Exporting and importing templates

Templates travel between environments through the **Messaging Templates** deployment step and the `OmnichannelMessageTemplate` recipe step. An imported template keeps its identifier, and every template needs a name and a body.

```json
{
  "steps": [
    {
      "name": "OmnichannelMessageTemplate",
      "Templates": [
        {
          "ItemId": "2hb7c4x9m1q6z3v8k5r0t2n7y",
          "Name": "Opening hours",
          "Body": "We are open Monday to Friday, 8am to 6pm."
        }
      ]
    }
  ]
}
```

Conversations and broadcasts do not travel: they are the workspace's record of what was said and sent, and replaying a broadcast would message its recipients again. Where a number's messages go travels with its entry point through the `ContactCenterEntryPoint` deployment step.

## Configuration

| Section | Settings |
| --- | --- |
| `CrestApps:Omnichannel:Messaging` | `InboxPageSize`, `ConversationLockTimeoutSeconds`, `ConversationLockExpirationSeconds`, `OutboxBatchSize`, `MaxMessagesPerPassPerEndpoint`, `MaxInboundAttachmentBytes`, `MaxInboundAttachments`, `AttachmentLinkLifetimeHours` |
| `CrestApps:Omnichannel:Messaging:RoutedDistribution` | Routed (push) distribution tunables |
| `CrestApps:Omnichannel:Messaging:Sms:KeywordReplies` | `StopMessage`, `HelpMessage`, `StartMessage` |

## Adding a channel

A channel is a feature that implements `IMessagingChannel` (in `CrestApps.OrchardCore.Omnichannel.Messaging.Core`) and registers it:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMessagingChannel<WhatsAppMessagingChannel>();

        // Let tenants tick WhatsApp on the phone numbers they own.
        services.AddOmnichannelAddressCapability(OmnichannelAddressTypes.PhoneNumber, "WhatsApp", capability =>
        {
            capability.DisplayName = S["WhatsApp"];
        });
    }
}
```

The channel answers only what differs between channels:

| Member | Purpose |
| --- | --- |
| `Name`, `DisplayName`, `IconCssClass`, `Order` | Identity. `Name` is stored on every conversation, message and endpoint of the channel, so it must never change. |
| `Capabilities` | Subject line, media, delivery receipts, broadcasts, quiet hours, maximum body length. The composer and services adapt to them. |
| `NormalizeAddress`, `FormatAddress`, `IsValidAddress` | How an address is stored, shown and validated. The normalized address is the conversation key. |
| `SendAsync` | Hand one outbound message to the provider serving the sending endpoint. |
| `IsOptedOut`, `GetContactAddresses`, `FindContactIdsAsync`, `SearchContactIdsByAddressAsync` | How a CRM contact is reached, recognised and opted out on the channel. |

Inbound traffic reaches the workspace when the channel's receiver (a webhook, an event handler) calls `IMessagingInboundProcessor.ProcessAsync` with a normalized `OmnichannelMessage` whose `Channel` is the channel's name. The workspace then finds or creates the conversation, routes it, starts the first-response clock, stores the message and notifies the inbox. Rules that belong to one channel only (as the carrier keywords belong to SMS) go in an `IMessagingInboundHandler`. Delivery receipts go to `IMessagingConversationService.ApplyDeliveryReceiptAsync`, naming the channel.

A channel that carries files lists them in `Capabilities.Attachments`: the `Formats` it accepts (from `MessagingFileFormats`, such as `Images` or `Documents`, or its own `MessagingFileFormat`), `MaxCount`, `MaxTotalBytes`, and `ShrinkImagesToFit` when its carriers cap the message size. The composer then offers exactly those formats, the server refuses anything else, and `SendAsync` receives a signed public link to each file in `MessagingOutboundMessage.MediaUrls`. A format with a signature is matched by the file's bytes; one without (plain text) by its extension, and it is only ever served as a download. Only pictures are shown inline. On the way in, the receiver puts the provider's media links on `OmnichannelMessage.MediaReferences`, and the workspace copies the ones the channel carries into its own store. A provider that serves media only to its own account registers an `IMessagingMediaRequestAuthenticator` to sign those downloads. For SMS, a provider sends pictures by implementing `ISmsMediaDispatchProvider`, or, when its `ISmsProvider` cannot be changed, through an `ISmsMediaSender` registered under the provider's name.

Everything else — routing, ownership, SLA, templates, broadcasts, retries, AI hand-off, permissions and the UI — is shared, so a new channel gets all of it without writing any. That includes its endpoints: the workspace stores every messaging channel's endpoint address in the channel's normalized form (so inbound traffic matches it), validates it with the channel's `IsValidAddress`, and adds the inbound-routing editor to it.

A step-by-step build guide for AI agents and developers — contracts, wiring and the files to update — lives in the repository at `.agents/skills/crestapps-messaging-channel`.

## Upgrading from the SMS Portal

The SMS-only **SMS Portal** feature (`CrestApps.OrchardCore.Omnichannel.Sms.Portal`) has been replaced by the workspace and the SMS channel. To move a tenant across:

1. Enable **SMS Messaging Channel**. The old feature no longer exists, so it drops off the tenant on its own.
2. The first time the workspace is enabled, its migration imports what the portal stored: conversations (with their message history, which already lives in the shared message store), templates, broadcasts, the inbound routing of each SMS endpoint (which then becomes a text entry point), and the portal permissions granted to roles (each role gets the workspace permission that replaces it).
3. Update any appsettings entries: `CrestApps:Sms:Workspace` is now `CrestApps:Omnichannel:Messaging`, `CrestApps:Sms:RoutedDistribution` is now `CrestApps:Omnichannel:Messaging:RoutedDistribution`, and `CrestApps:Sms:Portal:KeywordReplies` is now `CrestApps:Omnichannel:Messaging:Sms:KeywordReplies`.
4. Agents turn their **Available** toggle back on. The routed-assignment availability was stored under the portal's name and is not carried over.
