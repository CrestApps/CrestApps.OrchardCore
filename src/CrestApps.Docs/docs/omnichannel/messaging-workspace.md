---
sidebar_label: Messaging Workspace
sidebar_position: 4
title: Omnichannel Messaging Workspace
description: A human-operated, channel-agnostic messaging inbox for Orchard Core. SMS is the first channel; email, WhatsApp, Messenger and others plug in as further channel features.
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

It is the human counterpart to [SMS Automation](sms), which lets an **AI agent** carry an SMS conversation on its own. Both can run on the same numbers. An automated activity handles a conversation until it hands off, and from then on the workspace owns the thread.

For the agent's step-by-step guide with screencasts, see [Messaging](../user-manual/messaging.md) in the user manual.

## The workspace

- **Customer list.** Every customer with a conversation, most recent first. A customer who wrote on several channels appears **once**, with the unread messages of all their channels added together and an icon for each channel they used. Filter by *All*, *Mine* or *Unassigned*, and by channel when more than one is enabled.
- **Channel tabs.** Above the open conversation, one icon per enabled channel:
  - the channel on screen is highlighted;
  - a channel with unread messages carries a **red badge** with the count, updated live;
  - the channel on screen also counts the customer's new messages that arrive while you are scrolled up reading
    history or looking at another browser tab, and clears once you are back at the bottom of the conversation;
  - a channel where the customer has an address but no conversation yet opens the composer on that channel;
  - a channel where the customer cannot be reached is greyed out.
- **Conversation.** The thread, the composer (canned-response templates, Enter to send), and the customer card with every contact record that matches the address.
- **Attachments.** Each channel says which files it carries, and the composer follows it: drag files onto the conversation, paste them into the message box, or pick them with the attach button beside the channel name (a picture icon on a channel that carries pictures only, a paperclip on one that carries other files too). The button's tooltip lists what the channel accepts, and anything else is refused. Attached files show above the message and can be removed before sending; a message can be only attachments. SMS carries pictures (see [Pictures (MMS)](#pictures-mms)); a channel such as email can list documents as well. In the thread, pictures show as thumbnails that open the full picture in a new tab, and any other file shows as a download with its name and size.
- **Favorites.** Mark the customers you message most as favorites, and keep them one click away:
  - **Add to favorites** is a button at the top of every conversation, beside **Transfer**, and on the **Customer** card. Once a customer is a favorite, the button reads **Favorite** and clicking it again removes them.
  - The **star button** beside the filter above the customer list opens your favorites; **Favorites** in the filter menu does the same and shows how many you have. Each favorite shows their latest conversation, or *No conversation yet: click to write*, which opens the composer to them.
  - Your favorites also sit in a row above the customer list, and starred customers carry a star in it. The list's search narrows both.
  - Favorites are your own: each agent keeps a separate list of up to 100 customers, and starring someone changes nothing for anyone else.
- **New message.** The compose button opens a mail-style composer in the conversation pane, beside the customer list: pick the address to send **From** (which decides the channel), search contacts reachable on that channel for **To**, add other addresses, and write the message. One recipient starts a conversation; several get a private conversation each.
- **Claim, transfer, close, spam, reopen.** The same on every channel. Replying to a conversation nobody holds claims it for you, under the same rules as **Claim**; replying never takes a conversation from the agent who already holds it.
- **Transfer.** **Transfer** in the conversation header hands the conversation to another person, searched by name, or, when Contact Center Work Distribution is enabled, sends it **back to a queue**'s shared inbox for any member to claim. It stays the same conversation, so its whole history goes with it, and the customer is not told. An optional note for the recipient is kept in the conversation's history and never sent to the customer.
  - Whoever holds the conversation may transfer it, and so may a supervisor with `ViewAllMessagingConversations`. A conversation nobody holds is claimed first; a supervisor can assign it directly.
  - Only people who can use the messaging workspace are offered, never the person who already holds it, and never the person asking for the transfer.
  - A queue's conversation handed to one of its members stays the queue's; handed to someone outside the queue, it becomes that person's own conversation, since they could not open it otherwise.
  - The recipient's list picks the conversation up at once, with a notice saying who sent it, on whichever admin page they are on; the sender's list drops it. The thread shows *Transferred from A to B* at the point it happened.
- **Broadcasts.** One message to many recipients as individual 1:1 threads (not a group chat), on any channel that supports them. See [Broadcasts and templates](#broadcasts-and-templates).
- **Available toggle.** An agent with an agent profile has an **Available** switch (*Accept routed conversations to your inbox*) above the customer list. When a queue's endpoint uses **Routed** distribution, conversations are pushed only to agents who are available with their inbox open.
- **Real time.** New messages, delivery receipts and assignment changes are pushed over the workspace's own SignalR hub. A new message for the open conversation is appended, one for the same customer on another channel badges that channel's tab, and any new message moves its customer to the top of the list with its unread count. If a push is missed (a dropped connection), the open conversation catches up within seconds and the customer list within half a minute on its own.
- **Notifications on every admin page.** Agents do not have to keep the inbox open to hear about new work. On any other admin page, a notice pops up at the bottom right when a customer writes on a conversation they can see (*New message from* the customer's address, with the start of the message), when a conversation is transferred to them, and when one is sent back to a queue they serve. Clicking the notice opens the conversation. Each notice closes on its own after a few seconds.
- **Menu count.** The **Messaging > Inbox** item of the admin menu shows how many conversations are waiting for you: the unread open conversations assigned to you, plus the unread open conversations nobody has taken yet in the queues you serve (in every queue, for a supervisor with `ViewAllMessagingConversations`). The parent **Messaging** item carries no number; its icon turns red while the count is above zero, so a collapsed menu still shows that something is waiting. The count updates as messages arrive and drops as soon as you open the conversation.

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
| **Omnichannel Channel Endpoints** | Every address you send from (an SMS number today; a mailbox or WhatsApp number later) is a [channel endpoint](management#channel-endpoint). It carries the provider and the inbound routing. |
| **Contact Center Agent Services** (dependency-only) | Operators are Contact Center **agent profiles**. A bare profile is created automatically the first time a permitted user opens the workspace, so no Contact Center administration is required. |
| **Orchard Core SignalR** | The workspace's own real-time hub. |
| **Contact Center Provider Webhook Inbox** *(SMS channel only)* | The durable inbox that inbound provider deliveries are committed to before they are processed, so a retried delivery is de-duplicated and none is lost. |
| **Orchard Core SMS** *(SMS channel only)* | The SMS provider abstraction and the tenant default provider. |

The workspace does **not** require Contact Center Voice, Work Distribution or the Agents administration, and it does not pull in the Omnichannel Management CRM.

## Setting up SMS

1. **Configure an SMS provider.** Enable at least one, for example [Telnyx SMS](../telephony/telnyx#telnyx-sms) or Twilio, and pick the tenant **default provider** at **Settings > Communication > SMS**.
2. **Add your numbers as SMS channel endpoints** in **Interaction Center > Management > Channel Endpoints** (see [Channel endpoints](../user-manual/channel-endpoints.md)). Each SMS endpoint can pin the **provider** that owns the number; leave it empty to use the tenant default.
3. **Set the inbound routing** on the endpoint:
   - **Target**: an **agent** (personal number) or a **queue** (department number).
   - **Distribution mode**: **Shared pool** (agents claim conversations) or **Routed** (pushed to an agent by the routed-distribution feature).
   - **Auto-reply**: an optional acknowledgement, sent at most once a day per conversation.
4. **Grant the permissions** below to the roles that staff the inbox.
5. **Point the provider webhook at Orchard Core** so inbound messages and delivery receipts arrive. Telnyx SMS maps `api/telnyx/webhook/sms` (see the [Telnyx SMS webhook](../telephony/telnyx#telnyx-sms)). The Twilio inbound webhook, `api/twilio/webhook/sms`, is mapped by the [SMS Omnichannel Automation](sms) feature, which depends on the AI features; enable it to receive Twilio texts in the workspace.
6. Open **Messaging → Inbox**.

A **Send SMS** button appears beside phone-number fields on admin pages. It opens the customer's existing SMS conversation, or the composer when there is none.

### SMS compliance

The SMS channel applies the carrier keywords, whatever the workspace is doing:

| Keyword | Synonyms | What happens | Default reply |
| --- | --- | --- | --- |
| **STOP** | STOPALL, UNSUBSCRIBE, CANCEL, END, QUIT | Closes the conversation, sets the contact's **Do not SMS** flag and sends one confirmation. | *You have been unsubscribed and will not receive further messages. Reply START to resubscribe.* |
| **START** | UNSTOP, YES | Reverses an opt-out and confirms. It is ignored when the contact is not opted out, so an ordinary "Yes" stays part of the conversation. | *You have been resubscribed and will receive messages again. Reply STOP to unsubscribe.* |
| **HELP** | INFO | Answers with the help reply. | *Reply STOP to unsubscribe. Message and data rates may apply.* |

A keyword silences the endpoint's auto-reply for that message. Replies can be customised under the `CrestApps:Omnichannel:Messaging:Sms:KeywordReplies` configuration section (`StopMessage`, `HelpMessage`, `StartMessage`).

SMS also observes **quiet hours**: outside the destination queue's business hours, in the contact's local time, the conversation shows a banner above the composer. The banner is a warning only; it never blocks sending. The `SendMessagesDuringQuietHours` permission only changes how the banner looks: without it, the banner adds that sending now may reach the contact at an unsociable hour.

### Outbound retries

An outbound message the provider does not accept is queued and retried after 1, 5, 15, and 60 minutes — five attempts in all — before it is marked failed. A refusal because the recipient opted out is never retried.

Some providers manage opt-outs themselves (Twilio's opt-out management on toll-free numbers and Messaging Services, Telnyx's STOP handling): they confirm the opt-out to the customer and refuse any further message to them, including the workspace's own STOP confirmation. That refusal is expected and is logged as information, not as a warning. Whenever a provider refuses a message because the recipient opted out, the contact is marked **Do not SMS** and the message is marked failed at once rather than retried.

### Pictures (MMS)

The SMS channel carries pictures in both directions: JPEG, PNG, GIF and WebP, up to 10 per message. A file is accepted as a picture only when its contents are one of those formats, whatever its name or the type the browser reports; anything else, including documents, is refused on SMS.

**Sending.** Carriers refuse picture messages much over 1 MB, so the composer shrinks larger photos to fit before sending: each picture gets its share of that budget, and a still picture is redrawn smaller as a JPEG until it fits. An animated GIF is never redrawn, because that would lose the animation, so a GIF over its share is refused. A picture message is sent through a provider that can carry pictures:

- **Telnyx** sends the pictures with the message.
- **Twilio** numbers send pictures through the workspace's own Twilio sender, which uses the account in **Settings > SMS** (OrchardCore's Twilio provider sends text only).
- Any other provider refuses a message with pictures rather than delivering the text alone, and the message is marked failed without retries.

The provider downloads each picture from the site, so the site must be reachable from the internet. The link is built from the **Base URL** in **Settings > General**, or from the address of the current request when no base URL is set. Messages retried later in the background, and messages sent from a site reached only by a local or internal address, need the base URL set to the public address. The links are signed, name nothing but the picture, and expire after `AttachmentLinkLifetimeHours` (72 by default); a retried message gets fresh ones. The information-level log line *Built a public picture link on (host)* shows which address the provider was given.

**Receiving.** When a customer sends a picture, the workspace copies it from the provider as the message arrives, because providers only keep it for a while. A picture over `MaxInboundAttachmentBytes` (10 MB by default), a file that is not a supported picture, or one the provider refuses to hand over is not kept; the thread then says an attachment could not be shown, and the text of the message is kept either way. Pictures sent to a Twilio number are downloaded with the tenant's Twilio credentials, so an account that requires authentication for media works as well. Each copied or skipped item is logged at the `Information` or `Warning` level with the provider's message id.

**Storage.** Pictures are kept in the tenant's own `App_Data` folder (`MessagingAttachments`), encrypted with the tenant's data protection keys, never in the public media library. In the workspace they are only shown to someone who may open the conversation they belong to.

## Hand-off from an automated conversation

While an automated (AI) activity is handling a contact on an endpoint, the workspace leaves that contact's messages to the automated agent, even when a workspace conversation for them already exists. When the automated agent hands off, the whole automated transcript is copied into the conversation, so the agent inherits every message, in order, each exactly once. Customer messages keep the provider's message id, which is how a message the thread already holds, or a provider's redelivery of one, is recognised and not recorded again. Threads written before this behaviour are shown with such duplicates collapsed. This works the same on every messaging channel.

## Permissions

| Permission | Grants |
| --- | --- |
| `UseMessagingWorkspace` | Use the workspace on the endpoints you own or serve. |
| `ViewAllMessagingConversations` | See every conversation (supervisors), and transfer any of them. The holder of a conversation can transfer it without this. |
| `SendGroupMessages` | Send broadcasts and multi-recipient messages. |
| `SendMessagesDuringQuietHours` | Changes the quiet-hours banner to a plain notice without the unsociable-hour warning. Sending is never blocked, with or without it. |
| `ManageMessaging` | Manage templates (**Messaging > Templates**). |

Permissions apply to every channel; there is no per-channel permission. An endpoint's inbound routing is edited on the endpoint itself, under **Interaction Center > Management > Channel Endpoints**, which requires the **Manage channel endpoints** (`ManageChannelEndpoints`) permission.

## Broadcasts and templates

**Messaging > Templates** (requires `ManageMessaging`) lists the canned responses agents insert from the composer. Each template has a **Name** and a **Body**.

**Messaging > Broadcasts** (requires `SendGroupMessages`) lists sent broadcasts with their channel, sending address, status, recipient, sent and failed counts. **New broadcast** asks for:

| Field | Description |
| --- | --- |
| **Name** | A label for the broadcast. |
| **Send from** | The channel endpoint to send from. The address you pick decides the channel. |
| **Recipients (contacts)** | Contacts reachable on the selected channel, searched by name or address. |
| **Additional addresses** | Other addresses, one per line or comma-separated. |
| **Message** | The text sent to every recipient. |

**Queue broadcast** sends it in the background; each recipient gets their own 1:1 thread and cannot see the others. A queued broadcast cannot be cancelled.

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

        // Let tenants add WhatsApp numbers as channel endpoints.
        services.AddChannelEndpointSource("WhatsApp", source =>
        {
            source.DisplayName = S["WhatsApp"];
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
2. The first time the workspace is enabled, its migration imports what the portal stored: conversations (with their message history, which already lives in the shared message store), templates, broadcasts, the inbound routing of each SMS endpoint, and the portal permissions granted to roles (each role gets the workspace permission that replaces it).
3. Update any appsettings entries: `CrestApps:Sms:Workspace` is now `CrestApps:Omnichannel:Messaging`, `CrestApps:Sms:RoutedDistribution` is now `CrestApps:Omnichannel:Messaging:RoutedDistribution`, and `CrestApps:Sms:Portal:KeywordReplies` is now `CrestApps:Omnichannel:Messaging:Sms:KeywordReplies`.
4. Agents turn their **Available** toggle back on. The routed-assignment availability was stored under the portal's name and is not carried over.
